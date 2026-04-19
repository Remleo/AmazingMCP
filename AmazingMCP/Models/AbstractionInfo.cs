namespace AmazingMCP.Models;

/// <summary>
/// A type that acts as a dependency target in the solution's dependency graph.
/// Can be an interface, abstract class, concrete class, static class, or external (NuGet) type.
/// </summary>
public record AbstractionInfo
{
    public required string FullName { get; init; }
    public required string Namespace { get; init; }
    public required string ProjectName { get; init; }

    /// <summary>null for external/NuGet types.</summary>
    public required string? SourceFilePath { get; init; }

    public required bool IsInterface { get; init; }
    public required bool IsAbstractClass { get; init; }
    public required bool IsStaticClass { get; init; }

    /// <summary>Full names of all known source-defined implementations.</summary>
    public required IReadOnlyList<string> Implementations { get; init; }

    /// <summary>
    /// For closed generic abstractions (e.g. ITracer&lt;FooService&gt;): the open generic display name
    /// (e.g. "Bwin...ITracer&lt;TService&gt;"). Null for non-generic or open generic abstractions.
    /// </summary>
    public string? OpenGenericFullName { get; init; }

    /// <summary>
    /// XML doc &lt;summary&gt; text extracted from the type's documentation comment.
    /// Null if no summary is present. Full text is preserved; truncation happens at the output layer.
    /// </summary>
    public string? XmlDocSummary { get; init; }
}
