namespace AmazingMCP.Models;

/// <summary>
/// Information about a single implementation class (or abstract/base class with a body).
/// Stores only direct dependencies found in this type's own body — not aggregated from base classes.
/// Use IDependencyAggregator.GetAllUsages() to get the full recursive picture.
/// </summary>
public record ImplementationInfo(
    string FullName,
    string Namespace,
    string ProjectName,
    string? SourceFilePath,
    /// <summary>Abstractions this class directly implements (interfaces + base classes).</summary>
    IReadOnlyList<string> ImplementedAbstractions,
    /// <summary>Base class chain excluding System.Object.</summary>
    IReadOnlyList<string> BaseClasses,
    /// <summary>Direct dependencies found by scanning this class's own body only.</summary>
    IReadOnlyList<AbstractionUsage> Dependencies,
    /// <summary>True if this class is a generic type (open or closed).</summary>
    bool IsGeneric = false);
