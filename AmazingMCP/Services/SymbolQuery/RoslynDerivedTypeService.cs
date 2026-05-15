using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Finds all types that derive from or implement a given target type.
/// </summary>
public static class RoslynDerivedTypeService
{


    /// <summary>
    /// Returns all types that derive from (class target) or implement (interface target) the given type.
    ///
    /// Scope rules:
    /// - Target from source → search only source types (have DeclaringSyntaxReferences).
    /// - Target from NuGet → search source types + NuGet types, excluding well-known framework types.
    ///
    /// For interface targets: checks <see cref="INamedTypeSymbol.AllInterfaces"/> (recursive).
    /// For class targets: walks the <see cref="INamedTypeSymbol.BaseType"/> chain.
    /// </summary>
    public static IReadOnlyList<INamedTypeSymbol> FindDerivedTypes(
        ICachedSolution cachedSolution,
        INamedTypeSymbol targetType)
    {
        var targetFullName = targetType.ToDisplayString();
        var targetIsFromSource = targetType.DeclaringSyntaxReferences.Length > 0;
        var isInterface = targetType.TypeKind == TypeKind.Interface;

        var results = new List<INamedTypeSymbol>();

        foreach (var candidate in RoslynTypeEnumerator.EnumerateAll(cachedSolution))
        {
            var candidateFullName = candidate.ToDisplayString();

            if (candidateFullName == targetFullName)
                continue;

            var candidateIsFromSource = candidate.DeclaringSyntaxReferences.Length > 0;

            if (targetIsFromSource)
            {
                if (!candidateIsFromSource)
                    continue;
            }
            else
            {
                if (!candidateIsFromSource && WellKnownFrameworkTypes.IsWellKnown(candidate))
                    continue;
            }

            var matches = isInterface
                ? InheritsInterface(candidate, targetFullName)
                : InheritsClass(candidate, targetFullName);

            if (matches)
                results.Add(candidate);
        }

        return results;
    }

    static bool InheritsInterface(INamedTypeSymbol candidate, string targetFullName)
    {
        foreach (var iface in candidate.AllInterfaces)
        {
            if (iface.ToDisplayString() == targetFullName)
                return true;
        }
        return false;
    }

    static bool InheritsClass(INamedTypeSymbol candidate, string targetFullName)
    {
        var current = candidate.BaseType;
        while (current is not null && current.SpecialType == SpecialType.None)
        {
            if (current.ToDisplayString() == targetFullName)
                return true;
            current = current.BaseType;
        }
        return false;
    }

}
