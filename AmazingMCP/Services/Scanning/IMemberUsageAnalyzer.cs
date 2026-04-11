using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.Scanning;

/// <summary>
/// Scans the body of a single class (not its base classes) and returns all RawUsages found —
/// types that are accessed via method calls, property reads/writes, or static calls.
/// </summary>
public interface IMemberUsageAnalyzer
{
    /// <summary>
    /// Analyzes the direct body of <paramref name="cls"/> (no base class traversal).
    /// Returns one RawUsage per unique dependency type found.
    /// </summary>
    Task<IReadOnlyList<RawUsage>> AnalyzeAsync(
        INamedTypeSymbol cls,
        Compilation compilation,
        CancellationToken ct);
}
