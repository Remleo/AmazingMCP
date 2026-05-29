using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery.Strategies;

/// <summary>
/// Groups all versions of each type into a <see cref="TypeVersionGroup"/>.
/// Deduplication key is the full type name — Merge accumulates additional versions into the group.
/// </summary>
public class VersionedTypeStrategy : ITypeEnumerationStrategy<TypeVersionGroup>
{
    public object GetKey(INamedTypeSymbol symbol, Version? version) =>
        symbol.ToDisplayString();

    public TypeVersionGroup Project(INamedTypeSymbol symbol, Version? version) =>
        new(symbol.ToDisplayString(), [(version, symbol)]);

    public TypeVersionGroup Merge(TypeVersionGroup existing, INamedTypeSymbol symbol, Version? version)
    {
        // Already have this version — keep existing
        if (existing.Versions.Any(v => v.Version == version))
            return existing;

        return existing with
        {
            Versions = [.. existing.Versions, (version, symbol)]
        };
    }
}
