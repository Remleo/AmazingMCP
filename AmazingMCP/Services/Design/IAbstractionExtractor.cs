using AmazingMCP.Models;
using AmazingMCP.Models.Design;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.Design;

/// <summary>
/// Builds AbstractionInfo records from type symbols or RawTypeInfo.
/// </summary>
public interface IAbstractionExtractor
{
    /// <summary>
    /// Builds an AbstractionInfo from a RawTypeInfo (no Roslyn dependency at call site).
    /// </summary>
    AbstractionInfo BuildAbstractionInfo(
        RawTypeInfo typeInfo,
        string projectName,
        IReadOnlyList<string> implementations);

    /// <summary>
    /// Builds an AbstractionInfo directly from a Roslyn symbol (used during Phase 2 source type collection).
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
