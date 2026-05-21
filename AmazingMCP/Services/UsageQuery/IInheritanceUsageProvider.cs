using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.Wildcard;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Finds all types that inherit from or implement the target type,
/// producing a <see cref="UsageMatch"/> per derived type with <see cref="UsageKind.Inheritance"/>.
/// </summary>
public interface IInheritanceUsageProvider
{
    IReadOnlyList<UsageMatch> FindMatches(
        ICachedSolution cachedSolution,
        string typeName,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? includePatterns,
        List<IWildcardPattern>? excludePatterns);
}
