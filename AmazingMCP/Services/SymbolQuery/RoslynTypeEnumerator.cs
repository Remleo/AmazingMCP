using AmazingMCP.Services.Wildcard;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Low-level helpers for enumerating named types from Roslyn namespace/type trees.
/// </summary>
public static class RoslynTypeEnumerator
{
    public static IEnumerable<INamedTypeSymbol> FindNamedTypes(INamespaceSymbol ns, IWildcardPattern pattern)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type:
                    if (pattern.IsMatch(type.ToDisplayString()) || pattern.IsMatch(type.Name))
                        yield return type;

                    foreach (var nested in FindNestedTypes(type, pattern))
                        yield return nested;
                    break;

                case INamespaceSymbol childNs:
                    foreach (var t in FindNamedTypes(childNs, pattern))
                        yield return t;
                    break;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> FindNestedTypes(INamedTypeSymbol parent, IWildcardPattern pattern)
    {
        foreach (var nested in parent.GetTypeMembers())
        {
            if (!IsNestedTypeVisible(nested))
                continue;

            if (pattern.IsMatch(nested.ToDisplayString()) || pattern.IsMatch(nested.Name))
                yield return nested;

            foreach (var deeper in FindNestedTypes(nested, pattern))
                yield return deeper;
        }
    }

    /// <summary>
    /// Enumerates all named types in a namespace tree, including nested types at any depth.
    /// No filtering is applied.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> EnumerateAll(INamespaceSymbol ns)
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
                    foreach (var t in EnumerateAll(childNs))
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
