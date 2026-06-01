# DependencyMap — Solution Dependency Map

## Purpose

`DependencyMapService` builds a complete dependency map of a C# solution via Roslyn analysis. The result is two dictionaries: abstractions and their implementations with details about dependency usage (method calls, property access, static calls).

The map is used as a data source for `ProjectDesignService`, `GetDetailedProjectDesignTool`, and `GetTypeDepsAndUsageTool`.

## What Goes Into the Map

### Abstractions (`Abstractions`)

An abstraction is a type that serves as a dependency target in the solution's dependency graph.

| Category | Inclusion criteria |
|---|---|
| Interface | Has at least one source-defined concrete class implementor |
| Closed generic interface | `IRepository<Animal>` — a separate entry for each closed combination |
| Abstract class | Always, if defined in the solution's source code |
| Base class | If it appears in the inheritance chain of a concrete class |
| Concrete class without interfaces | If discovered as a dependency of another class (via `EnsureAbstraction`), or has dependencies itself and is not an implementor of anything (standalone) |
| External (NuGet) type | Added with `SourceFilePath = null` if there is a dependency on it |

Excluded (via `ITypeFilter`):
- System types (`System.*`, `Microsoft.Extensions.Options.*`, `Microsoft.Extensions.Logging.*`, `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`)
- Specific types: `IDisposable`, `IAsyncDisposable`, `ICloneable`, `IComparable`, `IFormattable`, `IConvertible`, `IEquatable`, `IObservable`, `IObserver`, `IServiceProvider`, `System.Object`
- Enum, struct, primitives
- Types from test projects (projects with `Microsoft.NET.Test.Sdk` in `.csproj`)
- Partial classes are deduplicated by full name (the compilation owning the syntax tree is preferred)

### Implementations (`Implementations`)

A concrete or abstract (non-static) class is included in `Implementations` if at least one of the following conditions is met:
- Has at least one discovered dependency (usages > 0), OR
- Is an implementor of a known abstraction (via interfaces, base classes, or via `abstractionImplementors`), OR
- Is already registered as an abstraction in Phase 2

Classes with no usages that are not implementors and are not registered as abstractions are skipped.

For each implementation, the following is collected:
- Implemented abstractions (including those from base classes)
- Base class chain
- Direct dependencies (discovered by scanning the class body) — `List<AbstractionUsage>`

Each `AbstractionUsage` contains:
- Full name of the dependency type
- `IsStatic` flag (dependency via a static call)
- List of `MemberUsage` (specific method calls and property accesses)

## Build Algorithm

The build proceeds in 3 phases + a final synchronization step:

**Phase 1 — type collection**
All source-defined types are collected from compilations, excluding test projects. Partial classes are deduplicated — when a type appears in multiple compilations (via project references), the compilation owning the syntax tree is preferred. A `typeIndex` (dictionary by full name) is built for fast lookup.

**Phase 2 — initial abstraction set**

First, `abstractionImplementors` is built — a mapping of "abstraction → list of concrete class implementors" based on `GetAllImplementedAbstractions()` (interfaces + base classes across the full hierarchy).

Then the following are added to `abstractions`:
1. Interfaces with at least one source-defined implementor
2. Closed generic interfaces — those present in `abstractionImplementors` but not found among source-defined types (resolved via `AllInterfaces` of concrete classes)
3. Abstract classes — all source-defined, regardless of whether they have implementors
4. Base classes — from the inheritance chain of concrete classes (source-defined only, via `typeIndex`)

**Phase 3 — class body scanning**

All classes (concrete + abstract, excluding static) are scanned. For each:

1. `IMemberUsageAnalyzer.AnalyzeAsync()` scans the class body → `List<AbstractionUsage>`
2. It is determined whether the class is an implementor of anything:
   - via `GetAllImplementedAbstractions()` (interfaces/base classes), OR
   - via `abstractionImplementors` (the class may have been registered as an implementor of a base class)
3. **Skip**: if there are no usages, the class is not an implementor, and it is not registered as an abstraction — the class is skipped
4. Each discovered dependency is registered via `EnsureAbstraction()`:
   - Source-defined type → added with full information from `typeIndex`
   - NuGet/external type → resolved via `compilation.GetTypeByMetadataName()`, added with `SourceFilePath = null`
5. The class is written to `implementations` with direct dependencies (from its own body only)
6. **Standalone classes** (not an implementor of anything, but has dependencies):
   - If not yet in `abstractions` → registered as its own abstraction with `Implementations = [self]`
   - If already in `abstractions` (pre-registered via `EnsureAbstraction` by another class) with empty `Implementations` → fixed to `[self]`

**Final synchronization**

After Phase 3, implementation lists from `abstractionImplementors` are synchronized back into `abstractions`. This is needed because `abstractionImplementors` was built in Phase 2 from concrete classes, while `abstractions` may have been supplemented in Phase 3 via `EnsureAbstraction` — their `Implementations` may be empty or incomplete.

## Scanning Services

Class body scanning is performed by a set of services from `Services/Scanning/`. Each is responsible for its own type of syntax node.

### MemberUsageAnalyzer (scanning orchestrator)

`MemberUsageAnalyzer` analyzes only the direct body of a class (no base class traversal). For aggregating dependencies across the inheritance chain, `IDependencyAggregator` is used.

Algorithm:
1. A **self-type set** is built — a `HashSet` of the class's full name and all its base classes (up to `System.Object`). Used to filter self-calls.
2. For each `DeclaringSyntaxReference` of the class (partial class support):
   - `compilation.ContainsSyntaxTree()` is checked — syntax trees from other compilations are skipped
   - All `DescendantNodes()` are walked and three node types are processed:
     - `InvocationExpressionSyntax` → delegated to `IInvocationAnalyzer`
     - `MemberAccessExpressionSyntax` → delegated to `IMemberAccessAnalyzer.AnalyzeAccess()`
     - `AssignmentExpressionSyntax` → delegated to `IMemberAccessAnalyzer.AnalyzeAssignment()`
3. Results are grouped in `usageMap` by the dependency type's full name. `HashSet<MemberUsage>` ensures deduplication of identical usages.
4. For each result from the analyzers, common filters are applied:
   - `ITypeFilter.ShouldExclude()` — exclusion of system types
   - `selfTypes.Contains()` — exclusion of self-calls
   - For property access/set — additionally: interfaces only (POCO/DTO property reads create noise)

### InvocationAnalyzer

Analyzes `InvocationExpressionSyntax` — method calls. Returns `(ContainingType, MemberName, IsStatic)`.

Skipped `MethodKind` values:
- `Constructor`, `StaticConstructor`
- `PropertyGet`, `PropertySet` (handled by `MemberAccessAnalyzer`)
- `EventAdd`, `EventRemove`
- `UserDefinedOperator`, `Conversion`

Three processing branches:
1. **Extension method** (`ReducedExtension` or `ReducedFrom is not null`): returns the receiver type (the type of the expression before the dot), not the static class declaring the extension. `IsStatic = false`.
2. **Static call** (`method.IsStatic`): returns `ContainingType` (the static class). `IsStatic = true`.
3. **Instance call**: returns `ContainingType` (the interface or class where the method is declared). `IsStatic = false`.

### MemberAccessAnalyzer

Analyzes property accesses. Two methods:

**`AnalyzeAccess()`** — property reads (`PropertyGet`):
- Skips if parent is `InvocationExpressionSyntax` (already handled by `InvocationAnalyzer`)
- Skips if this is the left side of an `AssignmentExpression` (will be handled by `AnalyzeAssignment`)
- Resolves the symbol — works only with `IPropertySymbol`

**`AnalyzeAssignment()`** — property writes (`PropertySet`):
- Works only if the left side of the assignment is a `MemberAccessExpressionSyntax`
- Resolves the symbol — works only with `IPropertySymbol`

### TypeFilter

Determines which types are excluded from the dependency map. Two methods:

**`ShouldExclude(INamedTypeSymbol)`** — full check:
- `SpecialType != None` (primitives: `int`, `string`, `bool`, etc.)
- `TypeKind` — `Enum` or `Struct`
- Then delegates to `ShouldExcludeByName()`

**`ShouldExcludeByName(string)`** — name-based check (fast path without a symbol):
- Strips generic parameters before checking (`IOptions<T>` → `IOptions`)
- Checks by exact name: `IDisposable`, `IAsyncDisposable`, `ICloneable`, `IComparable`, `IFormattable`, `IConvertible`, `IEquatable`, `IObservable`, `IObserver`, `IServiceProvider`, `System.Object`
- Checks by prefix: `System.*`, `Microsoft.Extensions.Options.*`, `Microsoft.Extensions.Logging.*`, `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`

## Dependency Aggregation (DependencyAggregator)

`IDependencyAggregator.GetAllUsages()` recursively collects dependencies of an implementation and all its base classes. Usages for the same abstraction are merged (deduplicated by `MemberName` + `Kind`). Used in `ProjectDesignService` and MCP tools to get the full dependency picture.

## Test Project Filtering

`TestProjectFilter.ExcludeTestProjects()` excludes compilations of projects whose `.csproj` contains `Microsoft.NET.Test.Sdk`. Applied in `DependencyMapService` before type analysis. `WorkspaceProvider` loads all projects without filtering — filtering only happens in `DependencyMapService`.

## Caching

The `BuildMapAsync` result is cached in `IMemoryCache` by the full path to the solution file. Absolute expiration — 5 minutes.

## Service Architecture

`DependencyMapService` is the orchestrator, delegating work via DI:

| Service | Responsibility |
|---|---|
| `IWorkspaceProvider` | Loading and caching MSBuild workspace, incremental recompilation on file changes |
| `ITypeCollector` | Collecting source-defined types, base class chain, list of implemented abstractions |
| `ITypeFilter` | Determines which types are excluded from the map (system, enum, struct, etc.) |
| `IMemberUsageAnalyzer` | Class body scanning orchestrator: syntax node traversal, delegation to analyzers, grouping and deduplication of results |
| `IInvocationAnalyzer` | `InvocationExpression` analysis — regular, extension (receiver type), and static calls, `MethodKind` filtering |
| `IMemberAccessAnalyzer` | `MemberAccessExpression` (property get) and `AssignmentExpression` (property set) analysis, coordination with `InvocationAnalyzer` |
| `IAbstractionExtractor` | Building `AbstractionInfo`, resolving closed generic interfaces |
| `IDependencyAggregator` | Recursive dependency aggregation across base class chains |
| `TestProjectFilter` | Static helper — excludes test projects from the compilation list |

## Models

```
DependencyMapResult
├── Abstractions: IReadOnlyDictionary<string, AbstractionInfo>
│   ├── FullName, Namespace, ProjectName
│   ├── SourceFilePath  (null for NuGet types)
│   ├── IsInterface, IsAbstractClass, IsStaticClass
│   └── Implementations: IReadOnlyList<string>
└── Implementations: IReadOnlyDictionary<string, ImplementationInfo>
    ├── FullName, Namespace, ProjectName, SourceFilePath
    ├── ImplementedAbstractions: IReadOnlyList<string>
    ├── BaseClasses: IReadOnlyList<string>
    └── Dependencies: IReadOnlyList<AbstractionUsage>
        ├── AbstractionFullName
        ├── IsStatic
        └── Usages: IReadOnlyList<MemberUsage>
            ├── MemberName
            └── Kind: MethodCall | PropertyGet | PropertySet
```

## Tests

Tests are located in `Tests/AmazingMCP.Tests/` as a partial class `DependencyMapServiceTests`:

| File | Coverage |
|---|---|
| `DependencyMapServiceTests.cs` | Fixture setup, `Act()` |
| `DependencyMapServiceTests.Abstractions.cs` | Interfaces, excluded system, abstract classes, NuGet dependencies, test project filtering |
| `DependencyMapServiceTests.Implementations.cs` | Basic implementations, base class chain, multi-interface |
| `DependencyMapServiceTests.Dependencies.cs` | Interface deps, NuGet deps, IEnumerable element type detection |
| `DependencyMapServiceTests.MemberUsages.cs` | Method call, property get, base class inheritance |
| `DependencyMapServiceTests.Generics.cs` | Closed/open generics, constructor deps on generics, member usages |
| `DependencyMapServiceTests.IEnumerableNonGeneric.cs` | IEnumerable\<IMessageHandler\>, IEnumerable\<IAsyncEventHandler\> |
| `DependencyMapServiceTests.StandaloneWithDeps.cs` | Standalone classes without interfaces with dependencies, EnsureAbstraction ordering |

Additional tests:
- `GetDetailedProjectDesignToolTests.cs` — detailed view formatting
- `GetTypeDepsAndUsageToolTests.cs` — type search, wildcard, fuzzy search
