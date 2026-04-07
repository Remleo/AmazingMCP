using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Collects source-defined types from compilations and filters system interfaces.
/// </summary>
public interface ITypeCollector
{
    /// <summary>
    /// Collects all source-defined named types from the given compilations.
    /// </summary>
    List<SourceType> CollectSourceTypes(
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations);

    /// <summary>
    /// Returns true if the interface should be excluded from the dependency map (system types).
    /// </summary>
    bool IsExcludedInterface(string fullName);

    /// <summary>
    /// Gets all non-excluded interfaces implemented by a class, including those from base classes.
    /// </summary>
    List<string> GetAllImplementedAbstractions(INamedTypeSymbol cls);

    /// <summary>
    /// Gets the base class chain (excluding System.Object).
    /// </summary>
    List<string> GetBaseClassChain(INamedTypeSymbol cls);
}
