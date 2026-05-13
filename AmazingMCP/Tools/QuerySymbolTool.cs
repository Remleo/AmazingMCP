using System.ComponentModel;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class QuerySymbolTool(
    ISymbolQueryService symbolQueryService,
    SolutionResolver solutionResolver)
{
    [McpServerTool(Name = "query_symbol"), Description(
        "IMPORTANT: THIS TOOL CAN SEARCH TYPES AND MEMBERS FROM THIRD-PARTY NUGET PACKAGES. " +
        "Searches types (classes, interfaces, enums, structs), members (methods, properties, fields), extension methods, constants, enum values, etc. " +
        "across the solution including NuGet. " +
        "USE CASES: " +
        "1. Find a specific type or member by name — use an exact name like \"Animal\" or \"GetUser\". " +
        "2. MUST USE when exploring an unfamiliar topic or technology — use wildcards to cast a wide net, e.g. \"*Redis*Connection*\" finds all types, methods, extension methods, and constants whose name contains both words. " +
        "   You MUST prefer this over any file or text search: it is orders of magnitude faster, works across the entire solution and all NuGet packages at once, and for third-party NuGet packages it is the ONLY way to discover relevant symbols — source files simply do not exist. " +
        "3. Browse a namespace — use \"SomeLibrary.SubNamespace.*\" to list everything declared in that namespace: all types, members, and extensions. " +
        "   Useful for exploring an unfamiliar library or confirming what a namespace exposes.")]
    public async Task<string> QuerySymbol(
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description(
            "Name or wildcard pattern. Examples: " +
            "\"Animal\" — exact pure name match; " +
            "\"Get*\" — starts with; " +
            "\"*Repository\" — ends with; " +
            "\"*.Services.*Animal*\" — namespace + name; " +
            "\"*Redis*Connection*\" — topic/technology search across all types and members; " +
            "\"SomeNugetNamespace.*\" — all types in a NuGet namespace.")]
        string query,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null) return error!;

        return await symbolQueryService.QueryAsync(resolved, query, ct);
    }
}
