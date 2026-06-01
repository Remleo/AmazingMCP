# get_type_deps_and_usage — Type Dependency and Usage Lookup

## Purpose

Look up any type by name to see who implements it, what it depends on, and who uses it.
Ideal for impact analysis, understanding a specific interface, or tracing a dependency chain.

> **Note:** This tool is currently disabled (`[McpServerToolType]` is commented out). It is built on top of `DependencyMapService` — see [DependencyMap.md](DependencyMap.md) for the underlying data model.

## Input

| Parameter | Type | Required | Description |
|---|---|---|---|
| `solutionWorkspacePath` | `string` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `typeQuery` | `string` | Yes | Type query: full name, partial name, or `*` wildcard patterns |
| `solutionPath` | `string?` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

## Query matching

| Input | Behavior |
|---|---|
| `MyApp.Core.IAnimalRepository` | Exact match — looks up abstractions first, then implementations |
| `IAnimalRepository` | Partial name — fuzzy search with auto-applied `*...*` wrapping |
| `*Repository*` | Wildcard — matches all abstractions whose full name matches the pattern |

### Fuzzy search normalization

When no exact match is found and the query has no wildcards, `NormalizeForFuzzySearch` is applied:
- Wraps with `*...*` if not already wildcarded
- Generic parameters are replaced with `*` (e.g. `IRepository<Animal>` → `*IRepository<*>*`)

## Output format

### Abstraction result

For each matched abstraction:

```markdown
# MyApp.Core.Persistence.IAnimalRepository

## Implementations

### MyApp.App.Persistence.AnimalRepository
Depends on:
- MyApp.Core.Configuration.IDbConfig
  - ConnectionString {get}
  - GetTimeout()
- MyApp.Core.Logging.IAppLogger
  - LogInfo()

### MyApp.App.Persistence.CachedAnimalRepository
Depends on:
- MyApp.Core.Persistence.IAnimalRepository
  - FindById()
  - Save()
- Microsoft.Extensions.Caching.Memory.IMemoryCache
  - TryGetValue()
  - Set()

## Used by

### MyApp.Core.Services.IAnimalService
- MyApp.App.Services.AnimalService
  - FindById()
  - Save()

### (standalone)
- MyApp.App.Startup.CompositionRoot
  - FindById()
```

### Implementation result

When the query matches an implementation (not an abstraction):

```markdown
# MyApp.App.Persistence.AnimalRepository

Implements:
- MyApp.Core.Persistence.IAnimalRepository

## Depends on

- MyApp.Core.Configuration.IDbConfig
  - ConnectionString {get}
  - GetTimeout()

## Used by

### MyApp.Core.Services.IAnimalService
- MyApp.App.Services.AnimalService
  - FindById()
```

### "Used by" grouping

The "Used by" section groups consumers by their implemented abstraction:
- Types implementing an interface are grouped under that interface heading
- Types without interfaces are grouped under `### (standalone)`

### Member usage format

```
  - MethodName()           — method call
  - PropertyName {get}     — property getter
  - PropertyName {set}     — property setter
```

### Generic collapse

Open generic abstractions collapse their closed generic variants. For example, if `ITracer<TService>` is matched, all closed forms like `ITracer<FooService>` are merged into the open generic entry. Their implementations and usages are aggregated together.

### No results

```
No types found matching pattern `*FooBar*`.
```

Or with fuzzy fallback:
```
No exact match found for `FooBar`.
Fuzzy search with pattern `*FooBar*` also returned no results.
```

## Implementation

```
GetTypeDepsAndUsageTool.GetTypeDepsAndUsage()
├── ISolutionResolver.Resolve() — resolves solution path
├── IDependencyMapService.BuildMapAsync() → DependencyMapResult
└── FormatMarkdown()
    ├── Wildcard query → FindByWildcard() → FormatAbstractionResults()
    ├── Exact match in Abstractions → FormatAbstractionResults()
    ├── Exact match in Implementations → FormatImplementationResult()
    └── No match → PerformFallbackSearch()
        ├── NormalizeForFuzzySearch() — wraps with *, replaces generic params
        └── Searches both Abstractions and Implementations keys

FormatAbstractionResults()
├── GenericCollapseHelper.Collapse() — merges closed generics into open
├── BuildUsedByIndex() — reverse index: abstraction → implementations using it
├── For each abstraction:
│   ├── List implementations with their dependencies
│   │   └── IDependencyAggregator.GetAllUsages() — recursive dep collection
│   └── "Used by" section grouped by consumer's abstraction
└── Deduplication of already-printed implementations

FormatImplementationResult()
├── Show implemented abstractions
├── IDependencyAggregator.GetAllUsages() — full dependency tree
└── BuildUsedByIndex() → "Used by" section
```

## Typical agent workflow

```
1. get_type_deps_and_usage("IAnimalRepository")
   → see all implementations, their dependencies, and who uses this interface

2. get_type_deps_and_usage("*Repository*")
   → find all repository-related abstractions and their dependency graphs

3. get_symbol_details(fullTypeName)
   → drill into a specific type for member signatures
```
