using System.ComponentModel;
using System.Text;
using AmazingMCP.Configuration;
using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Workspace;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class QuerySymbolTool(
    RoslynSymbolService roslyn,
    SolutionResolver solutionResolver,
    IOptions<SymbolOptions> options)
{
    readonly SymbolOptions _options = options.Value;

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
        if (resolved is null)
            return error!;

        var results = await roslyn.QuerySymbolsAsync(resolved, query, ct);

        if (results.Count == 0)
            return $"No types or members matching '{query}' found.";

        var hasWildcard = query.Contains('*');

        if (hasWildcard)
            return FormatAndTruncate(results, query);

        var exactMatches = results.Where(r => IsExactMatch(r, query)).ToList();
        var partialMatches = results.Where(r => !IsExactMatch(r, query)).ToList();

        return FormatAndTruncate(exactMatches, partialMatches, query);
    }

    // ── Exact match detection ─────────────────────────────────────────────────

    static bool IsExactMatch(SymbolResult result, string query)
    {
        if (query.Contains('.'))
            return result.FullName.EndsWith(query, StringComparison.OrdinalIgnoreCase);

        return result.Name.Equals(query, StringComparison.OrdinalIgnoreCase);
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    string FormatAndTruncate(IReadOnlyList<SymbolResult> all, string query)
    {
        var sb = new StringBuilder();
        AppendGroup(sb, all);
        return Truncate(sb, query);
    }

    string FormatAndTruncate(
        IReadOnlyList<SymbolResult> exact,
        IReadOnlyList<SymbolResult> partial,
        string query)
    {
        var sb = new StringBuilder();

        if (exact.Count > 0)
            AppendGroup(sb, exact);

        if (partial.Count > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine($"--- {partial.Count} partial match(es) ---");
            AppendGroup(sb, partial);
        }

        return Truncate(sb, query);
    }

    /// <summary>
    /// Appends results grouped by declaring type for members, and standalone for types.
    /// </summary>
    static void AppendGroup(StringBuilder sb, IReadOnlyList<SymbolResult> results)
    {
        var typeResults = results.Where(r => r.DeclaringType is null).ToList();
        var memberResults = results.Where(r => r.DeclaringType is not null).ToList();

        foreach (var r in typeResults)
            sb.AppendLine(FormatTypeResult(r));

        var byType = memberResults
            .GroupBy(r => r.DeclaringType!.FullName)
            .OrderBy(g => g.Key);

        foreach (var group in byType)
        {
            var declaringType = group.First().DeclaringType!;
            sb.AppendLine(FormatTypeResult(declaringType));

            var byKind = group.GroupBy(r => r.Kind).OrderBy(g => g.Key);

            foreach (var kindGroup in byKind)
            {
                sb.AppendLine($"  [{PluralKind(kindGroup.Key)}]");
                foreach (var member in kindGroup)
                    sb.AppendLine(FormatMemberResult(member));
            }
        }
    }

    static string PluralKind(string kind) => kind switch
    {
        "Property" => "Properties",
        _ => kind + "s",
    };

    static string FormatTypeResult(SymbolResult r) =>
        r.SourceFilePath is not null
            ? $"[{r.Kind}] {r.FullName}  (source: {r.SourceFilePath}, line {r.DefinitionLine})"
            : $"[{r.Kind}] {r.FullName}  (assembly: {r.ContainingAssembly})";

    static string FormatMemberResult(SymbolResult r) =>
        r.SourceFilePath is not null
            ? $"    {r.FullName}  (line {r.DefinitionLine})"
            : $"    {r.FullName}";

    string Truncate(StringBuilder sb, string query)
    {
        var text = sb.ToString();
        var lines = text.Split('\n');

        if (lines.Length <= _options.QueryOutputLineLimit)
            return text.TrimEnd();

        var truncated = string.Join('\n', lines.Take(_options.QueryOutputLineLimit));
        return truncated.TrimEnd() +
               $"\n\n<<... Output truncated (total {lines.Length} lines). " +
               $"Please narrow your query — e.g. '*.Namespace.*Foo*Bar*'>>";
    }
}
