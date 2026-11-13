namespace AmazingMCP.Services.SymbolQuery;

public interface ISymbolQueryService
{
    Task<string> QueryAsync(
        string solutionPath,
        IReadOnlyList<string>? typeQuery,
        IReadOnlyList<string>? typeExclude,
        IReadOnlyList<string>? memberQuery,
        CancellationToken ct = default);
}
