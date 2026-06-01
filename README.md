# AmazingMCP

[![NuGet](https://img.shields.io/nuget/v/HoldMyCoolantMeatbag.AmazingMCP)](https://www.nuget.org/packages/HoldMyCoolantMeatbag.AmazingMCP)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

An MCP server that gives AI agents deep understanding of C# codebases via Roslyn — type search, dependency graphs, usage analysis, and architecture overviews, all from a live in-memory compilation.

**One server, any number of solutions.** Start it once and point it at any `.sln`/`.slnx` file per call — no restart needed when switching between projects.

## Installation

```bash
dotnet tool install -g HoldMyCoolantMeatbag.AmazingMCP
```

Requires .NET 10 SDK.

## Usage

```bash
AmazingMCP
# or on a custom port:
AmazingMCP --urls http://localhost:9000
```

The server starts on `http://localhost:7777` by default. Each tool call accepts a `solutionWorkspacePath` parameter — the directory containing your `.sln`/`.slnx` file. Switch between solutions freely without restarting.

### Command-line options

| Option | Default | Description |
|---|---|---|
| `--urls` | `http://localhost:7777` | Listening URL |
| `--Symbol:QueryOutputLineLimit` | `100` | Max output lines for `query_symbol` |
| `--ReadCs:ReadOutputMaxLength` | `20000` | Max output characters for `read_large_cs_file` |
| `--ProjectDesign:DetailsOutputMaxLength` | `30000` | Max output characters for `get_project_design_details` |
| `--ProjectDesign:DetailsXmlDocSummaryMaxLength` | `2000` | Max XML doc summary characters in `get_project_design_details` |
| `--QueryUsages:QueryMatchLimit` | `200` | Max usage matches for `query_usages` |
| `--Diagnostics:IncludeExceptionDetails` | `false` | Include full exception details in tool error responses |

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "AmazingMCP": {
      "command": "AmazingMCP"
    }
  }
}
```

## MCP Tools

| Tool | Description |
|---|---|
| `get_project_design` | High-level architecture map: abstraction groups by namespace and inter-group dependencies |
| `get_project_design_details` | Detailed view of abstractions and implementations for specified namespaces (supports `*` wildcard) |
| `query_symbol` | Find any type by name across the solution and NuGet packages |
| `get_symbol_details` | Full type info: properties, methods, base types, nested types (including NuGet) |
| `query_usages` | Find all usages of a type: method calls, property access, constructor calls, generic arguments |
| `read_cs_file_digest` | Structural outline of a `.cs` file: types and members with line numbers — no implementations |
| `read_large_cs_file` | Read specific members from large `.cs` files by name filter |
| `decompile_type` | Decompile any type from a NuGet assembly |
| `code_lens` | Resolve fully-qualified types for any line range in a `.cs` file |

## How It Works

- Opens `.sln`/`.slnx` files and compiles all projects in memory via MSBuild Workspaces
- Workspace is cached with file watchers — `.cs` changes trigger incremental recompilation, `.csproj`/`.sln` changes invalidate the full cache
- NuGet types are resolved and included in dependency analysis
- Partial classes are deduplicated correctly
- Test projects are automatically excluded from dependency analysis

## Documentation

- [ProjectDesign — architecture overview tool](docs/ProjectDesign.md)
- [QueryUsages — usage search](docs/QueryUsages.md)
- [FileStructure — file structure outline](docs/FileStructure.md)
- [DependencyMap — dependency map](docs/DependencyMap.md)

## Contributing

PRs and issues are welcome. Please open an issue before submitting a large change.

```bash
git clone https://github.com/your-username/AmazingMCP
cd AmazingMCP
dotnet build
dotnet test
```

## License

MIT
