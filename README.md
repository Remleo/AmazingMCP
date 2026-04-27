# AmazingMCP — Project Overview

## What is it

An MCP server (Model Context Protocol) built on ASP.NET Core (.NET 10) that gives AI agents the ability to analyze C# solutions via Roslyn.

The server opens `.sln`/`.slnx` files, compiles projects in memory, and enables type search, dependency map construction, and high-level architecture overview of a solution.

## Documentation

- [DependencyMap — solution dependency map](docs/DependencyMap.md)
- [ProjectDesign — high-level solution design map](docs/ProjectDesign.md)
- [FileStructure — file structure outline](docs/FileStructure.md)
- [QueryUsages — usage search across solution](docs/QueryUsages.md)

## MCP Tools

| Tool | Description |
|---|---|
| `get_project_design` | High-level map: abstraction groups by namespace and inter-group dependencies |
| `get_project_design_details` | Detailed view of abstractions and implementations for specified namespace groups (supports `*` wildcard) |
| `get_type_deps_and_usage` | Full dependency and usage info for a type (exact, wildcard, fuzzy search) |
| `query_symbol` | Type search by name (including NuGet), with partial match support |
| `get_symbol_info` | Detailed type info: properties, methods, base types, nested types (including NuGet) |
| `get_file_structure` | Structural outline of a `.cs` file: namespaces, types, members with line/column positions — no implementations |
| `query_usages` | Find all usages of a type across the solution: method calls, property/field access, constructor calls, generic arguments, parameter types |

## Stack

- .NET 10, ASP.NET Core (Minimal API)
- Microsoft.CodeAnalysis (Roslyn) 5.3.0 + MSBuild Workspaces
- ModelContextProtocol.AspNetCore 1.2.0
- HTTP transport (port 7777 in dev)

## Projects

| Project | Purpose |
|---|---|
| `AmazingMCP` | Main HTTP MCP server |
| `AmazingMCP.Launcher` | Stdio wrapper: launches the main server as a child process, communicates via stdio |
| `AmazingMCP.Tests` | Tests (NUnit + FluentAssertions + NSubstitute) |

## Key Features

- Test projects (with `Microsoft.NET.Test.Sdk`) are automatically excluded from dependency analysis
- NuGet types are tracked as dependencies (`SourceFilePath = null`) but do not create groups in ProjectDesign
- Partial classes are deduplicated (the compilation owning the syntax tree is preferred)
- Workspace is cached with file watchers: `.cs` files are recompiled incrementally, `.csproj`/`.sln` changes invalidate the cache
- `DependencyMapService` results are cached separately (sliding expiration 2 hours)
- Dependencies are aggregated recursively across base class chains via `IDependencyAggregator`
- `get_file_structure` uses Roslyn SyntaxTree parsing only (no compilation) — works on any `.cs` file without a solution context
