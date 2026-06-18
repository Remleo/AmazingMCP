using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Per-request cache mapping a concrete member symbol to the interface members it implicitly implements.
/// Avoids repeated AllInterfaces traversal for the same symbol across documents.
/// </summary>
sealed class InterfaceMemberCache : IInterfaceMemberCache
{
    readonly Dictionary<ISymbol, IReadOnlyList<ISymbol>> _cache = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Returns all interface members implicitly implemented by <paramref name="memberSymbol"/>.
    /// Returns empty list if the member does not implement any interface member.
    /// </summary>
    public IReadOnlyList<ISymbol> GetInterfaceMembers(ISymbol memberSymbol)
    {
        if (_cache.TryGetValue(memberSymbol, out var cached))
            return cached;

        var result = BuildInterfaceMembers(memberSymbol);
        _cache[memberSymbol] = result;
        return result;
    }

    static IReadOnlyList<ISymbol> BuildInterfaceMembers(ISymbol memberSymbol)
    {
        var containingType = memberSymbol.ContainingType;
        if (containingType is null || containingType.TypeKind == TypeKind.Interface)
            return [];

        List<ISymbol>? result = null;

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers())
            {
                var impl = containingType.FindImplementationForInterfaceMember(ifaceMember);
                if (impl is not null && SymbolEqualityComparer.Default.Equals(impl, memberSymbol))
                {
                    result ??= [];
                    result.Add(ifaceMember);
                }
            }
        }

        return result ?? (IReadOnlyList<ISymbol>)[];
    }
}
