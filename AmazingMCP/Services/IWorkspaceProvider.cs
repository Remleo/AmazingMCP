using AmazingMCP.Models;

namespace AmazingMCP.Services;

public interface IWorkspaceProvider
{
    Task<ICachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default);
}
