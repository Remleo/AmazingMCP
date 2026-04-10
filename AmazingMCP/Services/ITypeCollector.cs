using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Collects source-defined types from compilations.
/// </summary>
public interface ITypeCollector
{
    /// <summary>
    /// Collects all source-defined named types from the given compilations.
    /// </summary>
    List<SourceType> CollectSourceTypes(
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations);

    /// <summary>
    /// Gets all non-excluded interfaces and base classes implemented by a type,
    /// walking the full hierarchy.
    /// </summary>
    List<string> GetAllImplementedAbstractions(INamedTypeSymbol cls);

    /// <summary>
    /// Gets the base class chain (excluding System.Object).
    /// </summary>
    List<string> GetBaseClassChain(INamedTypeSymbol cls);
}
