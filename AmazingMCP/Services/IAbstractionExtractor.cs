using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Builds AbstractionInfo records from type symbols.
/// </summary>
public interface IAbstractionExtractor
{
    /// <summary>
    /// Builds an AbstractionInfo for a source-defined type symbol.
    /// </summary>
    AbstractionInfo BuildAbstractionInfo(
        INamedTypeSymbol symbol,
        string projectName,
        Dictionary<string, List<string>> implementors);

    /// <summary>
    /// Finds a closed generic interface symbol by searching implementors' AllInterfaces.
    /// </summary>
    INamedTypeSymbol? FindClosedGenericInterface(
        string ifaceName,
        List<SourceType> classes);

    /// <summary>
    /// Resolves the project name for a closed generic interface by finding
    /// the project where the open generic definition is declared.
    /// </summary>
    string ResolveProjectForClosedGeneric(
        INamedTypeSymbol closedGenericSymbol,
        List<SourceType> allTypes);

    /// <summary>
    /// Gets declared public/internal members (methods and properties) of a type.
    /// </summary>
    List<string> GetDeclaredMembers(INamedTypeSymbol symbol);
}
