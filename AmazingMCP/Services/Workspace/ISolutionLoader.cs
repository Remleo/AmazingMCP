using AmazingMCP.Models;

namespace AmazingMCP.Services.Workspace;

public interface ISolutionLoader
{
    Task<CachedSolution> LoadAsync(string solutionPath, CancellationToken ct = default);
}
