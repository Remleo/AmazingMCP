# AmazingMCP — MCP Server for .NET / C# Codebases

[![NuGet](https://img.shields.io/nuget/v/HoldMyCoolantMeatbag.AmazingMCP)](https://www.nuget.org/packages/HoldMyCoolantMeatbag.AmazingMCP)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

An MCP server that gives AI agents deep understanding of C# codebases via Roslyn — type search, dependency graphs, usage analysis, and architecture overviews, all from a live in-memory compilation.

## Installation

```bash
dotnet tool install -g HoldMyCoolantMeatbag.AmazingMCP
```

Requires .NET 10 SDK.

## MCP Tools

| Tool | Description |
|---|---|
| `query_symbol` | Find any type by name across the solution and NuGet packages |
| `get_symbol_details` | Full type info: properties, methods, base types, nested types (including NuGet) |
| `query_usages` | Find all usages of a type across the solution: method calls, constructor calls, property/field read and write, generic arguments and constraints, return types, parameter types, inheritance, `nameof`, `typeof`, `is`/`as`. Supports predicate filtering and scan scope control |
| `read_cs_file_digest` | Token-efficient entry point for large `.cs` files (hundreds or thousands of lines): returns a structural outline — types and members with line numbers, no implementations. Use this first, then fetch only the members you need with `read_large_cs_file` |
| `read_large_cs_file` | Read specific member implementations from a `.cs` file by name filter — use after `read_cs_file_digest` to load only what's relevant instead of the entire file |
| `decompile_type` | Decompile any type from a NuGet assembly to C# source — no external tools required, ILSpy is built in |
| `code_lens` | Resolve fully-qualified types for any line range in a `.cs` file: local variables, field/property types, method call signatures, object creations, and declarations — all from the Roslyn semantic model |
| `get_project_design` | High-level architecture map: abstraction groups by namespace and inter-group dependencies |
| `get_project_design_details` | Detailed view of abstractions and implementations for specified namespaces (supports `*` wildcard) |

## Features

- **One server, any number of solutions** — start it once and point it at any project per call, no restart needed when switching between solutions.
- **Live in-memory compilation** — opens `.sln`/`.slnx` via MSBuild Workspaces and compiles all projects in memory. All tools run against a real Roslyn semantic model, not text search.
- **Incremental cache** — workspace is cached with file watchers. `.cs` changes trigger incremental recompilation; `.csproj`/`.sln` changes invalidate the full cache. First call per solution is slow; subsequent calls are instant.
- **NuGet-aware** — NuGet types are fully resolved and searchable alongside source types. `query_symbol`, `get_symbol_details`, and `decompile_type` work on any referenced package.

## Usage

```bash
AmazingMCP <options>

# example:
AmazingMCP --urls=http://localhost:7777 --Symbol:QueryOutputLineLimit=50 --ReadCs:ReadOutputMaxLength=50000

# see all options:
AmazingMCP --help
```

The server starts on `http://localhost:7777` by default.

### Command-line options

| Option | Default | Description |
|---|---|---|
| `--urls` | `http://localhost:7777` | Listening URL |
| `--Symbol:QueryOutputLineLimit` | `100` | Max output lines for `query_symbol` |
| `--ReadCs:ReadOutputMaxLength` | `20000` | Max output characters for `read_large_cs_file` |
| `--ProjectDesign:DetailsOutputMaxLength` | `30000` | Max output characters for `get_project_design_details` |
| `--ProjectDesign:DetailsXmlDocSummaryMaxLength` | `2000` | Max XML doc summary characters in `get_project_design_details` |
| `--QueryUsages:QueryMatchLimit` | `200` | Max usage matches for `query_usages` |
| `--Diagnostics:IncludeExceptionDetails` | `false` | Include full exception details in tool error responses (for diagnostics) |
| `--DisabledTools` | _(none)_ | Comma-separated list of tool names to disable (e.g. `code_lens,get_project_design`) |

### MCP Client Configuration

Add to your MCP client config (Claude Desktop, JetBrains AI, Kiro, etc.):

```json
{
  "mcpServers": {
    "AmazingMCP": {
      "type": "http",
      "url": "http://localhost:7777"
    }
  }
}
```

Then start the server manually in your terminal:

```bash
AmazingMCP --urls=http://localhost:7777 <other options>
```

Or add a launcher entry so the client starts the server automatically:

```json
{
  "mcpServers": {
    "AmazingMCP": {
      "type": "http",
      "url": "http://localhost:7777"
    },
    "AmazingMCP Launcher": {
      "command": "AmazingMCP",
      "args": ["--urls=http://localhost:7777"]
    }
  }
}
```


## Documentation

- [QuerySymbol — type and member search](docs/QuerySymbol.md)
- [QueryUsages — usage search](docs/QueryUsages.md)
- [CodeLens — type resolution for a code span](docs/CodeLens.md)
- [FileStructure — token-efficient file reading](docs/FileStructure.md)
- [DecompileType — NuGet assembly decompilation](docs/DecompileType.md)
- [ProjectDesign — architecture overview tool](docs/ProjectDesign.md)
- [DependencyMap — dependency map](docs/DependencyMap.md)

## Contributing

PRs and issues are welcome. Please open an issue before submitting a large change.

```bash
git clone https://github.com/remleo/AmazingMCP
cd AmazingMCP
dotnet build
dotnet test
```

## License

MIT
