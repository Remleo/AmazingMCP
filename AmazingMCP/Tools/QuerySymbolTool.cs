using System.ComponentModel;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class QuerySymbolTool(
    ISymbolQueryService symbolQueryService,
    ISolutionResolver solutionResolver)
{
    [McpServerTool(Name = "query_symbol"), Description(
        "IMPORTANT: THIS TOOL CAN SEARCH TYPES AND MEMBERS FROM THIRD-PARTY NUGET PACKAGES. " +
        "Searches types (classes, interfaces, enums, structs) and members (methods, properties, fields, events, extension methods, constants, enum values) " +
        "across the solution including NuGet. At least one of typeQuery or memberQuery is required. " +
        "MODES: " +
        "typeQuery only — returns matching types; " +
        "memberQuery only — returns matching members of any type; " +
        "typeQuery + memberQuery — returns matching members declared in matching types (the types themselves are not returned). " +
        "Patterns inside one list are combined with OR; a pattern without '*' is a contains-match. " +
        "USE CASES: " +
        "1. Find a type by name: typeQuery [\"Animal\"]. Find a member by name: memberQuery [\"GetUser\"]. " +
        "2. MUST USE when exploring an unfamiliar topic or technology — cast a wide net with wildcards. Types and members are separate searches, so make two calls: typeQuery [\"*Redis*Connection*\"] and memberQuery [\"*Redis*Connection*\"]. " +
        "   You MUST prefer this over any file or text search: it is orders of magnitude faster, works across the entire solution and all NuGet packages at once, and for third-party NuGet packages it is the ONLY way to discover relevant symbols — source files simply do not exist. " +
        "3. Find members inside a namespace or type: typeQuery [\"*.Fixture.*\", \"*.Market.*\"], memberQuery [\"*Aggregate\", \"*Aggregated\"]. " +
        "4. Browse a namespace: typeQuery [\"SomeLibrary.SubNamespace.*\"] lists its types; add memberQuery [\"*\"] to list their members. " +
        "5. Skip tests or generated code: typeExclude [\"*.Tests.*\"].")]
    public async Task<string> QuerySymbol(
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description(
            "Wildcard patterns matched against the full type name (with namespace) or the simple type name, combined with OR. Examples: " +
            "[\"Animal\"] — contains-match; " +
            "[\"I*Repository\"] — starts with I, ends with Repository; " +
            "[\"*.Services.*\"] — any type in a Services namespace; " +
            "[\"SomeNugetNamespace.*\"] — all types in a NuGet namespace.")]
        string[]? typeQuery = null,
        [Description(
            "Wildcard patterns matched against the full or simple type name; matching types and all their members are skipped. " +
            "Example: [\"*.Tests.*\", \"*.Generated.*\"].")]
        string[]? typeExclude = null,
        [Description(
            "Wildcard patterns matched against the simple member name, combined with OR. Examples: " +
            "[\"GetUser\"] — contains-match; " +
            "[\"Get*\"] — starts with; " +
            "[\"*Aggregate\", \"*Aggregated\"] — several name variants.")]
        string[]? memberQuery = null,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null) return error!;

        return await symbolQueryService.QueryAsync(resolved, typeQuery, typeExclude, memberQuery, ct);
    }
}
