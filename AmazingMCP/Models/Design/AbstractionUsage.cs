namespace AmazingMCP.Models.Design;

/// <summary>
/// Represents a dependency on an abstraction, including how its members are used.
/// </summary>
public record AbstractionUsage(
    string AbstractionFullName,
    /// <summary>True when the dependency is accessed via a static type reference.</summary>
    bool IsStatic,
    IReadOnlyList<MemberUsage> Usages);
