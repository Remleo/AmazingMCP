namespace AmazingMCP.Services.UsageQuery;

public interface IQueryUsagesService
{
    Task<string> QueryAsync(
        string solutionPath,
        string typeName,
        string? predicate = null,
        IReadOnlyList<string>? scanInclude = null,
        IReadOnlyList<string>? scanExclude = null,
        CancellationToken ct = default);
}
