using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>
/// Analyzes constructor parameters to extract DI dependencies.
/// </summary>
public interface IConstructorAnalyzer
{
    /// <summary>
    /// Extracts constructor-injected dependencies from the class's public constructor.
    /// Unwraps IOptions&lt;T&gt;, IOptionsSnapshot&lt;T&gt;, IOptionsMonitor&lt;T&gt;, and IEnumerable&lt;T&gt;.
    /// </summary>
    List<ConstructorDependency> AnalyzeDependencies(INamedTypeSymbol cls);
}
