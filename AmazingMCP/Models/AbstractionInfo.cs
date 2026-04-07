namespace AmazingMCP.Models;

/// <summary>
/// Information about an abstraction (interface or standalone class).
/// </summary>
public record AbstractionInfo(
    string FullName,
    string Namespace,
    string ProjectName,
    string? SourceFilePath,
    bool IsInterface,
    /// <summary>
    /// Public/internal members declared on this abstraction.
    /// </summary>
    IReadOnlyList<string> DeclaredMembers,
    /// <summary>
    /// Full names of all known implementations.
    /// </summary>
    IReadOnlyList<string> Implementations);
