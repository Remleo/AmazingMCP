using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class RoslynSymbolService(IWorkspaceProvider workspaceProvider, IWildcardPatternFactory wildcardFactory)
{
    public async Task<IReadOnlyList<SymbolResult>> QuerySymbolsAsync(
        string solutionPath,
        string query,
        CancellationToken ct = default)
    {
        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);

        // If the query has no wildcards, wrap it as a contains-pattern: *query*
        var wildcardQuery = query.Contains('*') ? query : $"*{query}*";
        var pattern = wildcardFactory.CreateForTypeNames(wildcardQuery);

        var seen = new HashSet<SeenSymbolKey>();
        var results = new List<SymbolResult>();

        foreach (var (_, compilation) in solution.Compilations)
        {
            SymbolQueryCollector.CollectTypes(compilation.GlobalNamespace, pattern, seen, results);
            SymbolQueryCollector.CollectMembers(compilation.GlobalNamespace, pattern, seen, results);
        }

        return results;
    }

    /// <summary>
    /// Finds a single type by its fully-qualified name across all compilations.
    /// Supports CLR metadata notation (Foo`2), C# generic syntax (Foo&lt;T, TVal&gt;),
    /// and wildcard form (Foo&lt;*,*&gt;). Returns null if not found or ambiguous.
    /// </summary>
    public async Task<(INamedTypeSymbol? Symbol, string? Error, ICachedSolution Solution)> FindExactTypeAsync(
        string solutionPath,
        string fullTypeName,
        CancellationToken ct = default)
    {
        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);
        var pattern = wildcardFactory.CreateForTypeNames(TypeWildcardPatternBuilder.Build(fullTypeName));

        var seen = new HashSet<string>();
        var matches = new List<INamedTypeSymbol>();

        foreach (var (_, compilation) in solution.Compilations)
        {
            foreach (var symbol in RoslynTypeEnumerator.FindNamedTypes(compilation.GlobalNamespace, pattern))
            {
                if (seen.Add(symbol.ToDisplayString()))
                    matches.Add(symbol);
            }
        }

        return matches.Count switch
        {
            0 => (null, $"Type '{fullTypeName}' not found.", solution),
            1 => (matches[0], null, solution),
            _ => (null, $"Ambiguous: '{fullTypeName}' matched multiple types:\n" +
                        string.Join("\n", matches.Select(m => $"  {m.ToDisplayString()}")), solution)
        };
    }
}
