using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery.Strategies;
using AmazingMCP.Services.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Finds all types that derive from or implement a given target type.
/// </summary>
public class RoslynDerivedTypeService(
    IRoslynTypeProvider typeProvider,
    [FromKeyedServices(TypeEnumerationMode.AllInstances)] ITypeEnumerationStrategy<INamedTypeSymbol> allInstancesStrategy) : IDerivedTypeService
{
    /// <summary>
    /// Returns all types that derive from (class target) or implement (interface target) the given type.
    ///
    /// Scope rules:
    /// - Target from source → search only source types (have DeclaringSyntaxReferences).
    /// - Target from NuGet → search source types + NuGet types, excluding well-known framework types.
    ///
    /// Uses AllInstances enumeration so that a type from a newer NuGet version (with more interfaces)
    /// is not missed because an older version was deduplicated away.
    /// Results are deduplicated by full name after matching.
    /// </summary>
    public IReadOnlyList<INamedTypeSymbol> FindDerivedTypes(
        ICachedSolution cachedSolution,
        InheritanceSearchSymbol target)
    {
        var matched = new Dictionary<string, INamedTypeSymbol>();

        foreach (var candidate in typeProvider.GetAll(cachedSolution, allInstancesStrategy))
        {
            var candidateFullName = candidate.ToDisplayString();

            if (candidateFullName == target.FullName)
                continue;

            var candidateIsFromSource = candidate.DeclaringSyntaxReferences.Length > 0;

            if (target.IsFromSource)
            {
                if (!candidateIsFromSource)
                    continue;
            }
            else
            {
                if (!candidateIsFromSource && WellKnownFrameworkTypes.IsWellKnown(candidate))
                    continue;
            }

            var matches = target.IsInterface
                ? InheritsInterface(candidate, target.FullName, target.IsOpenGeneric)
                : InheritsClass(candidate, target.FullName, target.IsOpenGeneric);

            if (!matches)
                continue;

            matched.TryAdd(candidateFullName, candidate);
        }

        return [.. matched.Values];
    }

    static bool InheritsInterface(INamedTypeSymbol candidate, string targetFullName, bool targetIsOpenGeneric)
    {
        foreach (var iface in candidate.AllInterfaces)
        {
            var name = targetIsOpenGeneric
                ? iface.OriginalDefinition.ToDisplayString()
                : iface.ToDisplayString();

            if (name == targetFullName)
                return true;
        }
        return false;
    }

    static bool InheritsClass(INamedTypeSymbol candidate, string targetFullName, bool targetIsOpenGeneric)
    {
        var current = candidate.BaseType;
        while (current is not null && current.SpecialType == SpecialType.None)
        {
            var name = targetIsOpenGeneric
                ? current.OriginalDefinition.ToDisplayString()
                : current.ToDisplayString();

            if (name == targetFullName)
                return true;

            current = current.BaseType;
        }
        return false;
    }
}
