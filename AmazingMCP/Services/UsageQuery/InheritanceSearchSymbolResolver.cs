using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.SymbolQuery.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Resolves a fully-qualified type name into an <see cref="InheritanceSearchSymbol"/>.
/// Uses Versioned enumeration: when a type exists in multiple NuGet versions, the symbol
/// from the highest version is used (<see cref="TypeVersionGroup.Best"/>).
/// If the type is not found directly and the name is a closed generic, falls back to the
/// open generic definition with IsOpenGeneric = false.
/// </summary>
public class InheritanceSearchSymbolResolver(
    IRoslynTypeProvider typeProvider,
    [FromKeyedServices(TypeEnumerationMode.Versioned)] ITypeEnumerationStrategy<TypeVersionGroup> versionedStrategy) : IInheritanceSearchSymbolResolver
{
    public InheritanceSearchSymbol? Resolve(ICachedSolution cachedSolution, string typeName)
    {
        var match = typeProvider.GetAll(cachedSolution, versionedStrategy)
            .FirstOrDefault(g => g.FullName == typeName);

        if (match is not null)
            return FromSymbol(match.Best);

        return ResolveClosedGeneric(cachedSolution, typeName);
    }

    static InheritanceSearchSymbol FromSymbol(INamedTypeSymbol symbol) =>
        new(
            FullName: symbol.ToDisplayString(),
            IsFromSource: symbol.DeclaringSyntaxReferences.Length > 0,
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            IsOpenGeneric: symbol.IsGenericType
                && SymbolEqualityComparer.Default.Equals(symbol, symbol.OriginalDefinition));

    InheritanceSearchSymbol? ResolveClosedGeneric(ICachedSolution cachedSolution, string typeName)
    {
        var angleBracketIndex = typeName.IndexOf('<');
        if (angleBracketIndex < 0)
            return null;

        var baseName = typeName[..angleBracketIndex];

        var openDef = typeProvider.GetAll(cachedSolution, versionedStrategy)
            .FirstOrDefault(g => g.FullName.StartsWith(baseName + "<", StringComparison.Ordinal));

        if (openDef is null)
            return null;

        var symbol = openDef.Best;

        return new InheritanceSearchSymbol(
            FullName: typeName,
            IsFromSource: symbol.DeclaringSyntaxReferences.Length > 0,
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            IsOpenGeneric: false);
    }
}
