namespace AmazingMCP.Models;

/// <summary>
/// The complete dependency map for a solution.
/// </summary>
public record DependencyMapResult(
    IReadOnlyDictionary<string, AbstractionInfo> Abstractions,
    IReadOnlyDictionary<string, ImplementationInfo> Implementations,
    /// <summary>
    /// Maps closed generic abstraction full names to their open generic full names.
    /// E.g. "Ns.ITracer&lt;FooService&gt;" → "Ns.ITracer&lt;TService&gt;"
    /// Only contains entries where the closed generic has no source-defined implementations
    /// (i.e. it was collapsed into the open generic group).
    /// </summary>
    IReadOnlyDictionary<string, string>? ClosedToOpenGenericMap = null);
