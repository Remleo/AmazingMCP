using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.UsageQuery;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Finds all types that derive from or implement a given target type.
/// </summary>
public interface IDerivedTypeService
{
    IReadOnlyList<INamedTypeSymbol> FindDerivedTypes(ICachedSolution cachedSolution, InheritanceSearchSymbol target);
}
