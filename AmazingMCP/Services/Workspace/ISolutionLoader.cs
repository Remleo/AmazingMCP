using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;

namespace AmazingMCP.Services.Workspace;

public interface ISolutionLoader
{
    Task<CachedSolution> LoadAsync(string solutionPath, CancellationToken ct = default);
}
