namespace AmazingMCP.Models;

/// <summary>
/// A type that acts as a dependency target in the solution's dependency graph.
/// Can be an interface, abstract class, concrete class, static class, or external (NuGet) type.
/// </summary>
public record AbstractionInfo(
    string FullName,
    string Namespace,
    string ProjectName,
    /// <summary>null for external/NuGet types.</summary>
    string? SourceFilePath,
    bool IsInterface,
    bool IsAbstractClass,
    bool IsStaticClass,
    /// <summary>Full names of all known source-defined implementations.</summary>
    IReadOnlyList<string> Implementations,
    /// <summary>
    /// For closed generic abstractions (e.g. ITracer&lt;FooService&gt;): the open generic display name
    /// (e.g. "Bwin...ITracer&lt;TService&gt;"). Null for non-generic or open generic abstractions.
    /// </summary>
    string? OpenGenericFullName = null);
