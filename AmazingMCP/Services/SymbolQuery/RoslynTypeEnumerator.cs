using AmazingMCP.Models.Workspace;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Low-level helpers for enumerating named types from Roslyn namespace/type trees.
/// </summary>
public static class RoslynTypeEnumerator
{
    /// <summary>
    /// Enumerates all named types across all compilations in the solution,
    /// deduplicating by fully-qualified display name.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> EnumerateAll(ICachedSolution cachedSolution)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (_, compilation) in cachedSolution.Compilations)
        {
            foreach (var type in EnumerateAllInCompilation(compilation.GlobalNamespace))
            {
                if (seen.Add(type.ToDisplayString()))
                    yield return type;
            }
        }
    }

    /// <summary>
    /// Enumerates all named types in a single namespace tree, including nested types at any depth.
    /// No filtering or deduplication is applied.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> EnumerateAllInCompilation(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type:
                    yield return type;
                    foreach (var nested in EnumerateNested(type))
                        yield return nested;
                    break;

                case INamespaceSymbol childNs:
                    foreach (var t in EnumerateAllInCompilation(childNs))
                        yield return t;
                    break;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol parent)
    {
        foreach (var nested in parent.GetTypeMembers())
        {
            if (!IsNestedTypeVisible(nested))
                continue;

            yield return nested;
            foreach (var deeper in EnumerateNested(nested))
                yield return deeper;
        }
    }

    static bool IsNestedTypeVisible(INamedTypeSymbol type)
    {
        if (type.Locations.Any(l => l.IsInSource))
            return true;

        return type.DeclaredAccessibility is not (Accessibility.Private or Accessibility.ProtectedAndInternal);
    }
}

