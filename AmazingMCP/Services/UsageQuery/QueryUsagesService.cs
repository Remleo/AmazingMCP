namespace AmazingMCP.Services.UsageQuery;

public class QueryUsagesService(
    IUsageProvider usageProvider,
    IUsageResultFormatter formatter) : IQueryUsagesService
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

        return formatter.Format(matches, truncated);
    }
}
