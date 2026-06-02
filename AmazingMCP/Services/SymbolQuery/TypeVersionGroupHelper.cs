using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Shared version-selection and banner-building logic for <see cref="TypeVersionGroup"/>,
/// used by both get_type_details and decompile_type.
/// </summary>
static class TypeVersionGroupHelper
{
    /// <summary>
    /// Picks the symbol for the requested version, falling back to <see cref="TypeVersionGroup.Best"/>.
    /// </summary>
    public static INamedTypeSymbol ResolveBest(TypeVersionGroup group, string? requestedVersion)
    {
        if (requestedVersion is not null && Version.TryParse(requestedVersion, out var parsed))
        {
            var match = group.Versions.FirstOrDefault(v => v.Version == parsed);
            if (match.Symbol is not null)
                return match.Symbol;
        }

        return group.Best;
    }

    /// <summary>
    /// Builds the version banner for the displayed symbol, terminated with a blank line.
    /// Returns an empty string when the group has no version info worth showing.
    /// </summary>
    public static string BuildBanner(TypeVersionGroup group, INamedTypeSymbol displayed)
    {
        if (group.Versions.Count <= 1 && group.Versions.All(v => v.Version is null))
            return string.Empty;

        var versions = group.Versions
            .Select(v => v.Version)
            .OrderByDescending(v => v)
            .Select(v => v?.ToString() ?? "source")
            .ToList();

        var displayedVersion = group.Versions
            .FirstOrDefault(v => SymbolEqualityComparer.Default.Equals(v.Symbol, displayed))
            .Version?.ToString() ?? "source";

        if (group.Versions.Count > 1)
            return $"// ⚠ WARNING: This type exists in multiple versions: {string.Join(", ", versions)}\n" +
                   $"// Showing version: {displayedVersion}. To see another version, pass version=\"<version>\" parameter.\n\n";

        return $"// NuGet version: {displayedVersion}\n\n";
    }
}
