using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Resolves a fully-qualified type name into an <see cref="InheritanceSearchSymbol"/>.
/// If the type is found directly — uses it. If not found and the name is a closed generic,
/// falls back to the open generic definition with IsOpenGeneric = false.
/// </summary>
public class InheritanceSearchSymbolResolver : IInheritanceSearchSymbolResolver
{
    public InheritanceSearchSymbol? Resolve(ICachedSolution cachedSolution, string typeName)
    {
        var directMatch = RoslynTypeEnumerator.EnumerateAll(cachedSolution)
            .FirstOrDefault(t => t.ToDisplayString() == typeName);

        if (directMatch is not null)
            return FromSymbol(directMatch);

        return ResolveClosedGeneric(cachedSolution, typeName);
    }

    static InheritanceSearchSymbol FromSymbol(INamedTypeSymbol symbol) =>
        new(
            FullName: symbol.ToDisplayString(),
            IsFromSource: symbol.DeclaringSyntaxReferences.Length > 0,
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            IsOpenGeneric: symbol.IsGenericType
                && SymbolEqualityComparer.Default.Equals(symbol, symbol.OriginalDefinition));

    static InheritanceSearchSymbol? ResolveClosedGeneric(ICachedSolution cachedSolution, string typeName)
    {
        var angleBracketIndex = typeName.IndexOf('<');
        if (angleBracketIndex < 0)
            return null;

        var baseName = typeName[..angleBracketIndex];

        var openDef = RoslynTypeEnumerator.EnumerateAll(cachedSolution)
            .FirstOrDefault(t => t.IsGenericType
                && t.ToDisplayString().StartsWith(baseName + "<", StringComparison.Ordinal));

        if (openDef is null)
            return null;

        // Use closed generic name but metadata from open generic definition
        return new InheritanceSearchSymbol(
            FullName: typeName,
            IsFromSource: openDef.DeclaringSyntaxReferences.Length > 0,
            IsInterface: openDef.TypeKind == TypeKind.Interface,
            IsOpenGeneric: false);
    }
}
