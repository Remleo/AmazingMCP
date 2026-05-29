using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery.Strategies;

/// <summary>
/// Returns one <see cref="INamedTypeSymbol"/> per unique full name.
/// First encountered instance wins; version is irrelevant.
/// </summary>
public class SimpleTypeStrategy : ITypeEnumerationStrategy<INamedTypeSymbol>
{
    public object GetKey(INamedTypeSymbol symbol, Version? version) =>
        symbol.ToDisplayString();

    public INamedTypeSymbol Project(INamedTypeSymbol symbol, Version? version) => symbol;

    public INamedTypeSymbol Merge(INamedTypeSymbol existing, INamedTypeSymbol symbol, Version? version) => existing;
}
