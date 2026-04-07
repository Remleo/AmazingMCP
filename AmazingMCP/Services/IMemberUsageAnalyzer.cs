using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Analyzes method/property usages on injected dependencies within a class and its base classes.
/// </summary>
public interface IMemberUsageAnalyzer
{
    /// <summary>
    /// Finds unique member usages (method calls, property get/set) on constructor dependencies
    /// within the class body and all base classes.
    /// </summary>
    Task<List<MemberUsage>> AnalyzeUsagesAsync(
        INamedTypeSymbol cls,
        List<ConstructorDependency> ctorDeps,
        Compilation compilation,
        CancellationToken ct);
}
