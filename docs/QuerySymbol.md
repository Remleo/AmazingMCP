# query_symbol / get_symbol_details — Symbol Lookup

## Purpose

Two tools for discovering and inspecting types across the solution and NuGet packages.

The recommended workflow is: `query_symbol` to find the type and its fully qualified name, then `get_symbol_details` to get its full member list.

---

## query_symbol

Searches types, members, extension methods, constants, and enum values across the entire solution including NuGet packages. Works against a live Roslyn compilation — no text search.

### Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `query` | Yes | Name or wildcard pattern |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

### Query patterns

| Pattern | Behavior |
|---|---|
| `Animal` | No wildcard → wrapped as `*Animal*` (contains-match) |
| `Get*` | Starts with |
| `*Repository` | Ends with |
| `*.Services.*Animal*` | Namespace + name |
| `*Redis*Connection*` | Topic/technology search across all types and members |
| `SomeNugetNamespace.*` | All types in a NuGet namespace |

Queries without `*` are automatically wrapped as `*query*` — so `Animal` finds `IAnimalRepository`, `AnimalService`, etc.

### Output format

Results are split into **exact matches** (name equals query) and **partial matches** (name contains query), separated by `--- N partial match(es) ---`.

Types are shown as:
```
[Class] MyApp.Core.Animal  (source: C:\...\Animal.cs, line 12)
[Interface] MyApp.Core.IAnimalRepository  (source: C:\...\IAnimalRepository.cs, line 5)
[Class] AutoMapper.MapperConfiguration  (assembly: AutoMapper [v12.0.1])
```

Members are grouped under their declaring type:
```
[Class] MyApp.Core.AnimalService  (source: ...)
  [Methods]
    MyApp.Core.AnimalService.GetById(int id)  (line 25)
    MyApp.Core.AnimalService.SaveAsync(Animal animal)  (line 40)
  [Properties]
    MyApp.Core.AnimalService.Name  (line 18)
```

Output is truncated at `--Symbol:QueryOutputLineLimit` (default 100 lines) with a hint to narrow the query.

### What is searched

- All types from all projects in the solution (source-defined)
- All types from all referenced NuGet assemblies
- For each type: methods (`MethodKind.Ordinary` only), properties, fields, events, enum values
- Extension methods are included as members of their declaring static class

### What is NOT searched

- Constructors (use `get_symbol_details` to see them)
- Private members of source types are included; private members of NuGet types are excluded
- Well-known framework types (BCL, ASP.NET Core, etc.) are excluded from member search to reduce noise

### Implementation

```
SymbolQueryService
└── RoslynSymbolService.QuerySymbolsAsync()
    ├── IWorkspaceProvider.GetSolutionAsync()     — loads/reuses cached MSBuild workspace
    ├── IRoslynTypeProvider.GetAll(versionedStrategy) — enumerates all types across compilations
    │   └── VersionedTypeStrategy                 — groups same type from multiple assemblies by version
    └── SymbolWalker (per type group)
        ├── CollectType()   — matches type full name or short name against pattern
        └── CollectMembers() — matches member names against pattern
            ├── Methods: MethodKind.Ordinary only
            ├── Properties, Fields, Events
            └── Enum values (IFieldSymbol with HasConstantValue)
```

---

## get_symbol_details

Returns detailed information about a type by its fully qualified name. Works for both source-defined types and NuGet types.

### Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `fullTypeName` | Yes | Fully qualified type name. Supports C# generic syntax (`List<T>`) and CLR metadata notation (`List\`1`) |
| `memberFilters` | No | Wildcard filters to show only matching members (e.g. `["*Get*", "Create*"]`). Constructors are always included when filters are specified |
| `version` | No | NuGet version to show. When omitted, the highest available version is shown |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

### What is returned

For **classes and interfaces**:
- All visible members (public, internal, protected): properties, methods, fields, events, operators
- Constructors
- Nested public/internal types
- Base type (recursively described, unless it's a well-known framework type)
- Implemented interfaces (recursively described, unless well-known)
- Known implementors / derived types (from source code)

For **enums**: underlying type + all values with their numeric constants.

### Compact mode

When a type has more than 25 visible members (or `memberFilters` would still leave >25), the output switches to **compact mode**: only member names are listed, without signatures. A note is shown:
```
// NOTE: This type has too many members (47). Only member names are shown.
// To see full signatures, pass memberFilters, e.g.: memberFilters: ["*Get*", "Create*", "MemberFullName"]
```

Inherited types are always shown in compact mode when the parent type triggered compact mode.

### Well-known framework type filtering

Base types and interfaces from these namespaces are shown as `(skipped — well-known framework type)` instead of being recursively described:
- `System.*`, `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`
- Specific types: `IDisposable`, `IAsyncDisposable`, `IEquatable`, `IComparable`, `IObservable`, etc.

### Member format

Members are formatted using `SymbolDisplayFormat.MinimallyQualifiedFormat` with:
- Accessibility modifier
- `abstract` / `virtual` / `override` / `static` / `readonly` / `const` modifiers
- Return type + name + parameters (with default values)
- Generic type parameters

Properties show accessor visibility: `{ get; private set; }`, `{ get; init; }`, etc.

### Version resolution

When a NuGet type exists in multiple versions (e.g. the same package referenced by different projects), `TypeVersionGroupHelper.ResolveBest()` picks the highest version. A banner is shown at the top:
```
// NuGet version: 12.0.1
```

### Implementation

```
SymbolInfoService
├── RoslynSymbolService.FindExactType()       — finds type by full name, handles generics and CLR notation
├── TypeVersionGroupHelper.ResolveBest()      — picks highest NuGet version
├── Describe()                                — recursive type description
│   ├── DescribeMembers()                     — CollectVisibleMembers() → AppendFullMembers() or AppendCompactMembers()
│   ├── AppendNestedTypes()                   — nested public/internal types
│   └── DescribeHierarchy()                   — base type + interfaces (recursive, with well-known skip)
├── DescribeDerivedTypes()                    — IDerivedTypeService.FindDerivedTypes() from source
└── XmlDocExtractor                           — XML doc summaries for NuGet types
```

---

## Typical agent workflow

```
1. query_symbol("Animal")                          → find the type, get its full name
2. get_symbol_details("MyApp.Core.Models.Animal")  → get all members and signatures
3. get_symbol_details("MyApp.Core.Models.Animal", memberFilters: ["*Get*"]) → filtered view
```
