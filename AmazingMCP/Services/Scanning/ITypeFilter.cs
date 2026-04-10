using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.Scanning;

/// <summary>
/// Determines whether a type should be excluded from the dependency map.
/// </summary>
public interface ITypeFilter
{
    /// <summary>
    /// Returns true if the type should be skipped (System.*, primitives, enums, structs, etc.).
    /// </summary>
    bool ShouldExclude(INamedTypeSymbol type);

    /// <summary>
    /// Returns true if the type full name should be skipped (fast path without symbol).
    /// </summary>
    bool ShouldExcludeByName(string fullName);
}
