# ProjectDesign — High-Level Solution Design Map

## Purpose

`ProjectDesignService` builds a high-level design map of a C# solution: groups of abstractions and dependencies between them. Unlike `DependencyMap`, which operates on individual types, ProjectDesign works at the group level — showing the architectural structure of a solution without details of specific classes and interfaces.

MCP tool: `get_project_design`

## What It Shows

A flat list of abstraction groups (no per-project split). For each group:

- Short name (relative to the project's root namespace)
- Full name (full namespace)
- Entry count (number of abstractions in the group)
- Dependencies on other groups (full namespaces of target groups)

## What Goes Into Groups

Groups are formed only from abstractions with `SourceFilePath != null` — i.e., types defined in the solution's source code. NuGet types (with `SourceFilePath = null`) do not form groups but participate in dependency resolution: if an implementation depends on a NuGet type, its namespace will appear in the corresponding group's `DependsOn`.

Types from test projects are excluded at the `DependencyMapService` level and do not appear in groups.

## Grouping

Abstractions are grouped by namespace. The root namespace is determined from `<RootNamespace>` in `.csproj`; if absent, the project name is used. The short group name is computed relative to the root namespace.

| Namespace | Root namespace | Short name |
|---|---|---|
| `MyApp.Core` | `MyApp.Core` | `(root)` |
| `MyApp.Core.Services` | `MyApp.Core` | `Services` |
| `MyApp.Core.Mapping.Tv2` | `MyApp.Core` | `Mapping.Tv2` |

## Inter-Group Dependencies

For each group, external dependencies are collected — those that go beyond the group's boundaries. Algorithm:

1. Take all abstractions in the group
2. For each abstraction, take all its implementations
3. For standalone abstractions (not an interface, not an abstract class) — also process as an implementation
4. For each implementation, `IDependencyAggregator.GetAllUsages()` recursively collects dependencies across the base class chain
5. Filter: keep only those that are known abstractions and do not belong to the current group
6. Resolve each dependency to the full namespace of the target group

NuGet dependencies are resolved to the external library's namespace (e.g., `AutoMapper`, `Microsoft.Extensions.Logging`) and appear in `DependsOn` alongside source groups.

## Example Output

```markdown
# Project Design

> Each group is shown as: `## ShortName (FullNamespace)`
> To get detailed info for specific groups, call `get_detailed_project_design` with `forNamespaces`.
> Use the `FullNamespace` value directly, or use `*` as a wildcard anywhere (e.g. `MyApp.App.*`, `*.Mapping`, `MyApp.*.Services`).

## Configuration (TestProject.Core.Configuration)
Entries count: 1

## EventHandling (TestProject.Core.EventHandling)
Entries count: 3

## GenericConsumers (TestProject.App.Services.GenericConsumers)
Entries count: 1
Depends on:
- → TestProject.Core.EventHandling
- → TestProject.Core.Persistence

## Logging (TestProject.Core.Logging)
Entries count: 1

## Mapping (TestProject.App.Mapping)
Entries count: 5
Depends on:
- → AutoMapper

## Mapping.Tv2 (TestProject.App.Mapping.Tv2)
Entries count: 1

## Mapping.Tv3 (TestProject.App.Mapping.Tv3)
Entries count: 1

## Mapping.Tv4 (TestProject.App.Mapping.Tv4)
Entries count: 1

## Messaging (TestProject.App.Messaging)
Entries count: 3
Depends on:
- → TestProject.App.Mapping
- → TestProject.Core.EventHandling

## Notifications (TestProject.Core.Notifications)
Entries count: 1

## Persistence (TestProject.Core.Persistence)
Entries count: 3

## Services (TestProject.App.Services)
Entries count: 3
Depends on:
- → TestProject.Core.Configuration
- → TestProject.Core.Persistence
- → TestProject.Core.Services

## Services (TestProject.Core.Services)
Entries count: 3
Depends on:
- → TestProject.Core.Persistence
```

## What Is NOT Included in the Output

- Names of specific abstractions (interfaces, classes)
- Implementation names
- Dependency details and member usages
- Namespace groups without their own source-defined abstractions
- Types from test projects
- NuGet types as standalone groups (only as targets in `DependsOn`)

## Architecture

`ProjectDesignService` uses `DependencyMapService` as a data source and `IDependencyAggregator` for recursive dependency collection:

```
ProjectDesignService
├── DependencyMapService.BuildMapAsync() → DependencyMapResult
├── Phase 1: group source abstractions by namespace
│   └── skip abstractions with SourceFilePath = null (NuGet)
├── Phase 2: lookup all abstractions (including NuGet) for dependency resolution
├── Phase 3: for each group — CollectExternalDependencies()
│   └── IDependencyAggregator.GetAllUsages() → recursive walk impl + base classes
├── ResolveRootNamespaces() → reads <RootNamespace> from .csproj files
├── ResolveOwningProject() → longest-prefix match namespace → project (for short name)
└── GetRelativeNamespace() → computes the short group name
```

`BuildFromDependencyMap` is an `internal static` method, enabling testing of the logic without async dependencies.

## Models

```
ProjectDesignResult
└── Groups: IReadOnlyList<AbstractionGroup>
    ├── FullName (full namespace)
    ├── Name (short name, "" for root)
    ├── EntryCount
    └── DependsOn: IReadOnlyList<string> (full namespaces of target groups, including NuGet)
```

## Tests

Tests are located in `Tests/AmazingMCP.Tests/ProjectDesignServiceTests.cs`:

| Area | Coverage |
|---|---|
| Flat groups | Source groups present, Infrastructure absent, sub-namespaces (Mapping.Tv2/Tv3/Tv4), GenericConsumers |
| NuGet exclusion | NuGet types do not create groups but appear in DependsOn |
| EntryCount | Entry counts in Services, Persistence groups |
| DependsOn | Cross-group deps, NuGet deps (AutoMapper), internal deps excluded, GenericConsumers → Persistence + EventHandling |
| ResolveOwningProject | Exact match, longest-prefix, fallback |
| GetRelativeNamespace | Root, child, empty root, different root |
| ExtractRootNamespace | With tag, without tag |
| Markdown | No project headers, group headers, full name, entries count label, depends on with full namespaces |
