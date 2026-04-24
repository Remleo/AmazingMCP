using System.ComponentModel;
using System.Text;
using AmazingMCP.Models;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class QuerySymbolTool
{
    const int OutputLineLimit = 100;

    [McpServerTool(Name = "query_symbol"), Description(
        "IMPORTANT: THIS TOOL CAN SEARCH TYPES AND MEMBERS FROM THIRD-PARTY NUGET PACKAGES. " +
        "Searches types (classes, interfaces, enums, structs), members (methods, properties), extension methods, constants, etc. " +
        "across the solution including NuGet. ")]
    public static async Task<string> QuerySymbol(
        RoslynSymbolService roslyn,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description(
            "Name or wildcard pattern. Examples: " +
            "\"Animal\" — exact pure name match; " +
            "\"Get*\" — starts with; " +
            "\"*Repository\" — ends with; " +
            "\"*.Services.*Animal*\" — namespace + name; " +
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

        // If query has wildcards — show all results flat.
        // If no wildcards — split into exact vs partial.
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
        // For members: match against the simple Name (no return type, no params)
        // For types: match against Name (no generic args) or tail of FullName
        if (query.Contains('.'))
            return result.FullName.EndsWith(query, StringComparison.OrdinalIgnoreCase);

        return result.Name.Equals(query, StringComparison.OrdinalIgnoreCase);
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    static string FormatAndTruncate(IReadOnlyList<SymbolResult> all, string query)
    {
        var sb = new StringBuilder();
        AppendGroup(sb, all);
        return Truncate(sb, query);
    }

    static string FormatAndTruncate(
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
        // Separate type-level results from member results
        var typeResults = results.Where(r => r.DeclaringType is null).ToList();
        var memberResults = results.Where(r => r.DeclaringType is not null).ToList();

        // Output standalone types
        foreach (var r in typeResults)
        {
            sb.AppendLine(FormatTypeResult(r));
        }

        // Output members grouped by declaring type
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
                {
                    sb.AppendLine(FormatMemberResult(member));
                }
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

    static string Truncate(StringBuilder sb, string query)
    {
        var text = sb.ToString();
        var lines = text.Split('\n');

        if (lines.Length <= OutputLineLimit)
            return text.TrimEnd();

        var truncated = string.Join('\n', lines.Take(OutputLineLimit));
        return truncated.TrimEnd() +
               $"\n\n<<... Output truncated (total {lines.Length} lines). " +
               $"Please narrow your query — e.g. '*.Namespace.*Foo*Bar*'>>";
    }
}
