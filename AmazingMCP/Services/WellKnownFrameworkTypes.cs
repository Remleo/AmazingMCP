using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Shared helper for identifying well-known framework types (System.*, Microsoft.*).
/// </summary>
public static class WellKnownFrameworkTypes
{
    static readonly string[] Prefixes = ["System.", "Microsoft."];

    public static bool IsWellKnown(INamedTypeSymbol type)
    {
        var name = type.ToDisplayString();
        foreach (var prefix in Prefixes)
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        return false;
    }
}
