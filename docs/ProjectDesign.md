# get_project_design / get_project_design_details — Architecture Overview

## Purpose

Two tools for understanding the high-level architecture of a C# solution.

- `get_project_design` — helicopter view: namespace groups and their inter-dependencies
- `get_project_design_details` — deep dive into specific namespace groups: abstractions, implementations, and dependency details

Both tools are built on top of `DependencyMapService` — see [DependencyMap.md](DependencyMap.md) for the underlying data model.

---

## get_project_design

Returns a flat list of abstraction groups (one per namespace) with their entry counts and dependencies on other groups.

### Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

### Output format

```markdown
# Project Design

> Each group is shown as: `## ShortName (FullNamespace)`
> To get detailed info for specific groups, call `get_project_design_details` with `forNamespaces`.
> Use the `FullNamespace` value directly, or use `*` as a wildcard anywhere (e.g. `MyApp.App.*`, `*.Mapping`, `MyApp.*.Services`).

## Services (MyApp.Core.Services)
Entries count: 3

Depends on:
- MyApp.Core.Persistence
- MyApp.Core.Configuration

## Persistence (MyApp.Core.Persistence)
Entries count: 2
```

### What goes into groups

- Only source-defined abstractions (`SourceFilePath != null`) form groups
- NuGet types do not form groups but appear in `Depends on` (e.g. `AutoMapper`, `Microsoft.Extensions.Logging`)
- Types from test projects are excluded
- Groups are formed by namespace; short name is relative to the project's root namespace (`<RootNamespace>` in `.csproj`, fallback to project name)
- Root namespace group is shown as `(root)`

### Inter-group dependencies

For each group, external dependencies are collected:
1. Take all abstractions in the group
2. For each abstraction, take all its implementations
3. For standalone abstractions (not interface/abstract class) — also process as implementation
4. For each implementation, `IDependencyAggregator.GetAllUsages()` recursively collects dependencies across the base class chain
5. Filter: keep only those that are known abstractions and do not belong to the current group
6. Resolve each dependency to the full namespace of the target group

### Implementation

```
ProjectDesignService
└── IProjectDesignProvider.BuildAsync()
    └── ProjectDesignProvider
        ├── DependencyMapService.BuildMapAsync()      — builds full dependency map
        ├── Phase 1: group source abstractions by namespace
        ├── Phase 2: for each group — CollectExternalDependencies()
        │   └── IDependencyAggregator.GetAllUsages()  — recursive walk impl + base classes
        ├── ResolveRootNamespaces()                   — reads <RootNamespace> from .csproj files
        └── GetRelativeNamespace()                    — computes short group name
```

---

## get_project_design_details

Deep-dives into specific namespace groups: shows each abstraction with its implementations and dependency details.

### Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `forNamespaces` | Yes | Namespace patterns to include. Supports `*` wildcard anywhere. At least one required |
| `includeDependencyUsage` | No | When `true`, shows which methods/properties are called on each dependency. Default: `false` |
| `includeImplementations` | No | When `true` (default), shows the list of implementations for each abstraction |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

### Output format

```markdown
# Project Design Details

> Namespaces: `MyApp.Core.Services`
> Abstractions found: 3

## MyApp.Core.Services.IAnimalService
> Manages animal lifecycle operations.

### Implementations
- MyApp.App.Services.AnimalService

### Depends on
- MyApp.Core.Persistence.IAnimalRepository
  - FindById()
  - Save()
- MyApp.Core.Logging.ILogger
  - LogInformation()
```

### Namespace pattern matching

Patterns are matched against the abstraction's **namespace** (not full name):
- `MyApp.Core.Services` — exact namespace match
- `MyApp.Core.*` — all namespaces starting with `MyApp.Core.`
- `*.Services` — all namespaces ending with `.Services`
- `*` — all namespaces

### Dependency usage format

When `includeDependencyUsage: true`, each dependency shows the member-level calls:
- `MethodName()` — method call
- `PropertyName {get}` — property getter
- `PropertyName {set}` — property setter

Closed generic dependencies are collapsed to their open generic form when the open generic exists in abstractions (e.g. `IRepository<Animal>` + `IRepository<Order>` → `IRepository<T>`).

### Output size control

Output is truncated at `--ProjectDesign:DetailsOutputMaxLength` (default 30 000 chars) with a hint to use more specific namespaces or disable `includeDependencyUsage`/`includeImplementations`.

XML doc summaries are truncated at `--ProjectDesign:DetailsXmlDocSummaryMaxLength` (default 2 000 chars).

### Implementation

```
ProjectDesignDetailsService
├── DependencyMapService.BuildMapAsync()
├── Filter abstractions by namespace patterns (wildcard match)
├── For each abstraction:
│   ├── Show XML doc summary (if present)
│   ├── List implementations (if includeImplementations)
│   └── Collect dependencies via IDependencyAggregator.GetAllUsages()
│       └── Collapse closed generics via GenericCollapseHelper
└── Truncate output if > DetailsOutputMaxLength
```

---

## Typical agent workflow

```
1. get_project_design()                                    → see all namespace groups and dependencies
2. get_project_design_details(["MyApp.Core.Services"])     → drill into a specific group
3. get_project_design_details(["MyApp.Core.*"], includeDependencyUsage: true) → full details with call info
```
