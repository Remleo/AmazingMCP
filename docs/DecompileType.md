# decompile_type — NuGet Assembly Decompilation

## Purpose

Decompiles a type from a NuGet assembly and returns its C# source code using ILSpy/ICSharpCode.Decompiler. Use this when you need to see the actual implementation of a third-party type — not just its public API.

For types defined in solution source files, use `read_cs_file_digest` / `read_large_cs_file` instead — the tool will return an error with the source file paths if you try to decompile a source type.

## Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `fullTypeName` | Yes | Fully qualified type name (e.g. `AutoMapper.MapperConfiguration`) |
| `memberFilters` | No | Wildcard filters to show only matching members (e.g. `["*Get*", "Create*"]`). Constructors and `using` directives are always included when filters are specified |
| `version` | No | NuGet version to decompile. When omitted, the highest available version is shown |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

## Output behavior

- **No filters, source ≤ max length** — returns full decompiled source
- **No filters, source > max length** — returns a digest (structural outline without bodies) with a note to use `memberFilters`
- **With filters** — returns only matching members + constructors + usings. If still too large, truncates and appends the digest
- **Source type** — returns an error with the source file paths

Max length is controlled by `--ReadCs:ReadOutputMaxLength` (default 20 000 chars).

A version banner is prepended:
```
// NuGet version: 12.0.1
```

## Implementation

```
DecompileTypeService
├── RoslynSymbolService.FindExactType()       — finds the type symbol in the Roslyn compilation
├── TypeVersionGroupHelper.ResolveBest()      — picks highest NuGet version
├── FindDllPath()                             — locates the .dll via PortableExecutableReference in compilations
├── CSharpDecompiler (ILSpy)                  — decompiles the type to C# source
│   └── IlspyFullTypeNameBuilder.Build()      — converts INamedTypeSymbol to ILSpy FullTypeName
├── FormatFullOutput()                        — returns source or digest if too large
└── FormatFilteredOutput()                    — applies FilteredSourceService with [filters + .ctor + usings]
    └── IFilteredSourceService.GetFilteredSource()
```

## Typical agent workflow

```
1. query_symbol("MapperConfiguration")              → find the full type name
2. decompile_type("AutoMapper.MapperConfiguration") → see the implementation
3. decompile_type("AutoMapper.MapperConfiguration", memberFilters: ["*Map*"]) → filtered view
```
