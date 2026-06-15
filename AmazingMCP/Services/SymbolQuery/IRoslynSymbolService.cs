using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;

namespace AmazingMCP.Services.SymbolQuery;

public interface IRoslynSymbolService
{
    Task<IReadOnlyList<SymbolResult>> QuerySymbolsAsync(
        string solutionPath,
        string query,
        IReadOnlyList<KindGroup>? kindGroups = null,
        CancellationToken ct = default);

    /// <summary>
    /// Finds a type by its fully-qualified name, returning all known versions grouped.
    /// Supports CLR metadata notation (Foo`2), C# generic syntax (Foo&lt;T, TVal&gt;),
    /// and wildcard form (Foo&lt;*,*&gt;).
    /// </summary>
    (TypeVersionGroup? Group, string? Error) FindExactType(
        ICachedSolution solution,
        string fullTypeName);
}
