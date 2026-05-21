using AmazingMCP.Models.Workspace;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Resolves a fully-qualified type name into an <see cref="InheritanceSearchSymbol"/>.
/// Handles both direct matches and closed generic type names.
/// </summary>
public interface IInheritanceSearchSymbolResolver
{
    InheritanceSearchSymbol? Resolve(ICachedSolution cachedSolution, string typeName);
}
