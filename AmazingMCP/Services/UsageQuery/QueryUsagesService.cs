using AmazingMCP.Models;
using AmazingMCP.Services.SymbolQuery;

namespace AmazingMCP.Services.UsageQuery;

public class QueryUsagesService(
    IUsageProvider usageProvider,
    IUsageResultFormatter formatter,
    IRoslynSymbolService roslyn) : IQueryUsagesService
{
    public async Task<string> QueryAsync(
        string solutionPath,
        string typeName,
        string? predicate = null,
        IReadOnlyList<string>? scanInclude = null,
        IReadOnlyList<string>? scanExclude = null,
        CancellationToken ct = default)
    {
        var (matches, error, truncated) = await usageProvider.QueryAsync(
            solutionPath, typeName, predicate, scanInclude, scanExclude, ct);

        if (error is not null)
            return $"Error: {error}";

        if (matches.Count == 0)
        {
            var suggestions = await FindTypeSuggestionsAsync(solutionPath, typeName, ct);
            return formatter.Format(matches, truncated, suggestions);
        }

        return formatter.Format(matches, truncated);
    }

    async Task<IReadOnlyList<SymbolResult>> FindTypeSuggestionsAsync(
        string solutionPath,
        string typeName,
        CancellationToken ct)
    {
        var simpleName = TypeNameHelper.GetSimpleName(typeName);

        if (string.IsNullOrEmpty(simpleName))
            return [];

        var candidates = await roslyn.QuerySymbolsAsync(solutionPath, simpleName, [KindGroup.Type], ct);

        return candidates
            .Where(c => c.Name.Equals(simpleName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
