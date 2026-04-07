namespace AmazingMCP.Models;

/// <summary>
/// Information about a single implementation class.
/// </summary>
public record ImplementationInfo(
    string FullName,
    string Namespace,
    string ProjectName,
    string? SourceFilePath,
    /// <summary>
    /// Interfaces this class implements (excluding well-known system ones).
    /// </summary>
    IReadOnlyList<string> ImplementedAbstractions,
    /// <summary>
    /// Base classes chain (excluding System.Object).
    /// </summary>
    IReadOnlyList<string> BaseClasses,
    /// <summary>
    /// Constructor-injected dependencies.
    /// </summary>
    IReadOnlyList<ConstructorDependency> Dependencies,
    /// <summary>
    /// Unique member usages on injected dependencies found in this class and its base classes.
    /// </summary>
    IReadOnlyList<MemberUsage> DependencyMemberUsages);
