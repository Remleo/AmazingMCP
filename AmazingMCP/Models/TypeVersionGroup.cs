using Microsoft.CodeAnalysis;

namespace AmazingMCP.Models;

/// <summary>
/// Groups all known versions of a single named type across solution compilations.
/// Version is null for source types or types without a detectable NuGet version.
/// </summary>
public record TypeVersionGroup(
    string FullName,
    IReadOnlyList<(Version? Version, INamedTypeSymbol Symbol)> Versions)
{
    /// <summary>The symbol from the highest NuGet version, or the only available symbol.</summary>
    public INamedTypeSymbol Best => Versions
        .OrderByDescending(v => v.Version)
        .First().Symbol;
}
