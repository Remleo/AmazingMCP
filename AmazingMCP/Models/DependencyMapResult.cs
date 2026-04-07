namespace AmazingMCP.Models;

/// <summary>
/// The complete dependency map for a solution.
/// </summary>
public record DependencyMapResult(
    IReadOnlyDictionary<string, AbstractionInfo> Abstractions,
    IReadOnlyDictionary<string, ImplementationInfo> Implementations);
