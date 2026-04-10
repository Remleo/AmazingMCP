using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Analyzes method/property usages on injected dependencies within a class and its base classes.
/// </summary>
public interface IMemberUsageAnalyzer
{
    /// <summary>
    /// Finds member usages (method calls, property get/set) on constructor dependencies,
    /// grouped by dependency type full name.
    /// </summary>
    Task<Dictionary<string, List<MemberUsage>>> AnalyzeUsagesAsync(
        INamedTypeSymbol cls,
        List<ConstructorDependency> ctorDeps,
        Compilation compilation,
        CancellationToken ct);
}
