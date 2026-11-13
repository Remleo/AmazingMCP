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

    public async Task<string> QueryAsync(
        string solutionPath,
        IReadOnlyList<string>? typeQuery,
        IReadOnlyList<string>? typeExclude,
        IReadOnlyList<string>? memberQuery,
        CancellationToken ct = default)
    {
        var hasTypeQuery = typeQuery is { Count: > 0 };
        var hasMemberQuery = memberQuery is { Count: > 0 };

        if (!hasTypeQuery && !hasMemberQuery)
            return "At least one of typeQuery or memberQuery must be specified. " +
                   "The former 'query' parameter is replaced by them: use typeQuery for types, memberQuery for members.";

        var results = await roslyn.QuerySymbolsAsync(solutionPath, typeQuery, typeExclude, memberQuery, ct);

        if (results.Count == 0)
        {
            var queryDescription = DescribeQuery(typeQuery, memberQuery);
            return $"No types or members matching {queryDescription} found.";
        }

        // Results are members when memberQuery is set, types otherwise
        var resultQuery = hasMemberQuery ? memberQuery! : typeQuery!;

        if (resultQuery.Any(q => q.Contains('*')))
            return FormatAndTruncate(results);

        var exactMatches = results.Where(r => IsExactMatch(r, resultQuery)).ToList();
        var partialMatches = results.Where(r => !IsExactMatch(r, resultQuery)).ToList();

        return FormatAndTruncate(exactMatches, partialMatches);
    }

    // ── Exact match detection ─────────────────────────────────────────────────

    static bool IsExactMatch(SymbolResult result, IReadOnlyList<string> queries) =>
        queries.Any(q => IsExactMatch(result, q));

    static bool IsExactMatch(SymbolResult result, string query)
    {
        if (query.Contains('.'))
            return result.FullName.EndsWith(query, StringComparison.OrdinalIgnoreCase);

        return result.Name.Equals(query, StringComparison.OrdinalIgnoreCase);
    }

    static string DescribeQuery(IReadOnlyList<string>? typeQuery, IReadOnlyList<string>? memberQuery)
    {
        var typeText = typeQuery is { Count: > 0 } ? $"typeQuery [{string.Join(", ", typeQuery)}]" : null;
        var memberText = memberQuery is { Count: > 0 } ? $"memberQuery [{string.Join(", ", memberQuery)}]" : null;
        string?[] parts = [typeText, memberText];
        var presentParts = parts.OfType<string>();
        return string.Join(" and ", presentParts);
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    string FormatAndTruncate(IReadOnlyList<SymbolResult> all)
    {
        var sb = new StringBuilder();
        AppendGroup(sb, all);
        return Truncate(sb);
    }

    string FormatAndTruncate(
        IReadOnlyList<SymbolResult> exact,
        IReadOnlyList<SymbolResult> partial)
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

        return Truncate(sb);
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

    string Truncate(StringBuilder sb)
    {
        var text = sb.ToString();
        var lines = text.Split('\n');

        if (lines.Length <= _options.QueryOutputLineLimit)
            return text.TrimEnd();

        var truncated = string.Join('\n', lines.Take(_options.QueryOutputLineLimit));
        return truncated.TrimEnd() +
               $"\n\n<<... Output truncated (total {lines.Length} lines). " +
               "Please narrow your query — e.g. typeQuery ['*.Namespace.*Foo*'], memberQuery ['*Bar*'] or typeExclude ['*.Tests.*']>>";
    }
}
