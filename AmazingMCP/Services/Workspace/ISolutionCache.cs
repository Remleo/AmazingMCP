using AmazingMCP.Models;

namespace AmazingMCP.Services.Workspace;

public interface ISolutionCache
{
    CachedSolution? TryGet(string solutionPath);
    void Set(string solutionPath, CachedSolution entry, Action<CachedSolution> onEvicted);
    void Invalidate(string solutionPath);
}
