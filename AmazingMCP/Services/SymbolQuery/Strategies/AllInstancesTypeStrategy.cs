using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery.Strategies;

/// <summary>
/// Returns one <see cref="INamedTypeSymbol"/> per unique (full name, version) pair.
/// Produces all versions of each type without grouping.
/// </summary>
public class AllInstancesTypeStrategy : ITypeEnumerationStrategy<INamedTypeSymbol>
{
    public object GetKey(INamedTypeSymbol symbol, Version? version) =>
        (symbol.ToDisplayString(), version);

    public INamedTypeSymbol Project(INamedTypeSymbol symbol, Version? version) => symbol;

    public INamedTypeSymbol Merge(INamedTypeSymbol existing, INamedTypeSymbol symbol, Version? version) => existing;
}
