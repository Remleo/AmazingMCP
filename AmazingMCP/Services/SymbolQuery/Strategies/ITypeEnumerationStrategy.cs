using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery.Strategies;

/// <summary>
/// Stateless strategy that controls how types are deduplicated and projected
/// during enumeration in <see cref="RoslynTypeProvider"/>.
/// </summary>
public interface ITypeEnumerationStrategy<T>
{
    /// <summary>
    /// Returns the deduplication key for the given symbol.
    /// Two symbols with the same key are considered duplicates — only one will be kept (or merged).
    /// <paramref name="version"/> is the NuGet package version, or null for source types
    /// or assemblies without a detectable NuGet version.
    /// </summary>
    object GetKey(INamedTypeSymbol symbol, Version? version);

    /// <summary>Creates a new result entry for a symbol seen for the first time.</summary>
    T Project(INamedTypeSymbol symbol, Version? version);

    /// <summary>
    /// Merges a newly encountered symbol into an existing result entry (same key, different instance).
    /// Return the updated entry.
    /// </summary>
    T Merge(T existing, INamedTypeSymbol symbol, Version? version);
}
