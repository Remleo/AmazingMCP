using AmazingMCP.Models;

namespace AmazingMCP.Services;

/// <summary>
/// Builds and caches the full dependency map for a solution.
/// </summary>
public interface IDependencyMapService
{
    /// <summary>
    /// Builds (or returns a cached) dependency map for the given solution file.
    /// </summary>
    Task<DependencyMapResult> BuildMapAsync(string solutionPath, CancellationToken ct = default);
}
