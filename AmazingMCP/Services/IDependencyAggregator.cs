using AmazingMCP.Models;

namespace AmazingMCP.Services;

/// <summary>
/// Aggregates AbstractionUsages recursively across an implementation's base class chain.
/// Each Implementation stores only its own direct dependencies; this service merges them all.
/// </summary>
public interface IDependencyAggregator
{
    /// <summary>
    /// Returns all AbstractionUsages for <paramref name="implFullName"/>, merging usages
    /// from the implementation itself and all base classes recursively.
    /// Usages for the same abstraction are merged (deduplicated by MemberName+Kind).
    /// </summary>
    IReadOnlyList<AbstractionUsage> GetAllUsages(string implFullName, DependencyMapResult map);
}
