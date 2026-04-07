using AmazingMCP.Models;

namespace AmazingMCP.Services;

public interface IWorkspaceProvider
{
    Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default);
}
