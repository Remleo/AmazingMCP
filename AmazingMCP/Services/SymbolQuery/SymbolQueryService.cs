using System.Text;
using AmazingMCP.Configuration;
using AmazingMCP.Models;
using Microsoft.Extensions.Options;

namespace AmazingMCP.Services.SymbolQuery;

public class SymbolQueryService(
    IRoslynSymbolService roslyn,
    IOptions<SymbolOptions> options) : ISymbolQueryService
{
    readonly SymbolOptions _options = options.Value;

    public async Task<string> QueryAsync(string solutionPath, string query, CancellationToken ct = default)
    {
        var results = await roslyn.QuerySymbolsAsync(solutionPath, query, ct: ct);

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

    static string FormatTypeResult(SymbolResult r)
    {
        var location = SourceLocationFormatter.FormatLocation(
            r.SourceFilePaths,
            r.ContainingAssembly,
            r.SourceFilePaths.Count == 1 ? r.DefinitionLine : null,
            r.NuGetVersions);

        return $"[{r.Kind}] {r.FullName}  ({location})";
    }

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
