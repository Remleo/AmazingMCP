using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Builds AbstractionInfo records from type symbols.
/// </summary>
public interface IAbstractionExtractor
{
    /// <summary>
    /// Builds an AbstractionInfo for a source-defined or external type symbol.
    /// </summary>
    AbstractionInfo BuildAbstractionInfo(
        INamedTypeSymbol symbol,
        string projectName,
        IReadOnlyList<string> implementations);

    /// <summary>
    /// Finds a closed generic interface symbol by searching implementors' AllInterfaces.
    /// </summary>
    INamedTypeSymbol? FindClosedGenericInterface(string ifaceName, List<SourceType> classes);

    /// <summary>
    /// Resolves the project name for a closed generic interface.
    /// </summary>
    string ResolveProjectForClosedGeneric(INamedTypeSymbol closedGenericSymbol, List<SourceType> allTypes);
}
