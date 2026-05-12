using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;

namespace AmazingMCP.Services.Workspace;

public interface IWorkspaceProvider
{
    Task<ICachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default);
}
