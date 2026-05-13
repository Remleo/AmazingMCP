namespace AmazingMCP.Services.SymbolQuery;

public interface ISymbolQueryService
{
    Task<string> QueryAsync(string solutionPath, string query, CancellationToken ct = default);
}
