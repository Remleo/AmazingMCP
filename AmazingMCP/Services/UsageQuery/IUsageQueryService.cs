using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;

namespace AmazingMCP.Services.UsageQuery;

public interface IUsageQueryService
{
    /// <summary>
    /// Traverses the solution and returns all usage matches where the type pattern matches
    /// and the optional predicate returns true.
    /// </summary>
    /// <param name="solutionPath">Absolute path to the .sln/.slnx file.</param>
    /// <param name="typePattern">Wildcard pattern matched against <see cref="QueryEntry.TypeName"/>. Required.</param>
    /// <param name="predicate">Optional C# expression evaluated as <c>Func&lt;QueryEntry, bool&gt;</c> with variable <c>x</c>.</param>
    /// <param name="scanInclude">Optional wildcard patterns for containing type names. Only matching types are traversed.</param>
    /// <param name="scanExclude">Optional wildcard patterns for containing type names. Matching types are skipped.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(IReadOnlyList<UsageMatch> Matches, string? Error, bool Truncated)> QueryAsync(
        string solutionPath,
        string typePattern,
        string? predicate,
        IReadOnlyList<string>? scanInclude,
        IReadOnlyList<string>? scanExclude,
        CancellationToken ct = default);
}
