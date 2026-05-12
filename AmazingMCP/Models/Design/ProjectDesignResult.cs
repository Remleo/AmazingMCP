namespace AmazingMCP.Models.Design
{
    /// <summary>
    /// High-level project design: flat list of abstraction groups and their inter-group dependencies.
    /// </summary>
    public record ProjectDesignResult(
        IReadOnlyList<AbstractionGroup> Groups);
}

namespace AmazingMCP.Models
{
    /// <summary>
    /// A group of abstractions with their external group dependencies.
    /// Currently grouped by namespace; other grouping factors may be added in the future.
    /// </summary>
    public record AbstractionGroup(
        /// <summary>
        /// Full namespace of the group (e.g. "TestProject.Core.Services").
        /// </summary>
        string FullName,
        /// <summary>
        /// Short display name (e.g. "Services"). Empty string for the root group.
        /// </summary>
        string Name,
        /// <summary>
        /// Number of entries (abstractions) in this group.
        /// </summary>
        int EntryCount,
        /// <summary>
        /// Groups that this group depends on (external dependencies resolved to group full names).
        /// </summary>
        IReadOnlyList<string> DependsOn);
}
