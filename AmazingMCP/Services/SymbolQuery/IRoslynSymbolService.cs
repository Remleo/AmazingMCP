using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;

namespace AmazingMCP.Services.SymbolQuery;

public interface IRoslynSymbolService
{
    /// <summary>
    /// Searches types and members by wildcard patterns; patterns inside one list are combined with OR.
    /// <para>
    /// Only <paramref name="typeQuery"/> returns matching types. Only <paramref name="memberQuery"/> returns
    /// matching members of any type. Both return matching members declared in matching types.
    /// </para>
    /// </summary>
    /// <param name="solutionPath">Path to the .sln/.slnx file.</param>
    /// <param name="typeQuery">Patterns matched against the full or simple type name.</param>
    /// <param name="typeExclude">Patterns matched against the full or simple type name; matching types and their members are skipped.</param>
    /// <param name="memberQuery">Patterns matched against the simple member name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">Both <paramref name="typeQuery"/> and <paramref name="memberQuery"/> are empty.</exception>
    Task<IReadOnlyList<SymbolResult>> QuerySymbolsAsync(
        string solutionPath,
        IReadOnlyList<string>? typeQuery,
        IReadOnlyList<string>? typeExclude,
        IReadOnlyList<string>? memberQuery,
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
