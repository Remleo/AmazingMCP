using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery.Strategies;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AmazingMCP.Services.SymbolQuery;

public class RoslynSymbolService(
    IWorkspaceProvider workspaceProvider,
    IWildcardPatternFactory wildcardFactory,
    IRoslynTypeProvider typeProvider,
    [FromKeyedServices(TypeEnumerationMode.Versioned)] ITypeEnumerationStrategy<TypeVersionGroup> versionedStrategy)
{
    public Task<ICachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default) =>
        workspaceProvider.GetSolutionAsync(solutionPath, ct);

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

        foreach (var group in typeProvider.GetAll(solution, versionedStrategy))
        {
            var allVersions = group.Versions.Select(v => v.Version).ToList();

            SymbolWalker.CollectType(group.Best, pattern, seen, results, allVersions);
            SymbolWalker.CollectMembers(group.Best, pattern, seen, results, allVersions);
        }

        return results;
    }

    /// <summary>
    /// Finds a type by its fully-qualified name, returning all known versions grouped.
    /// Supports CLR metadata notation (Foo`2), C# generic syntax (Foo&lt;T, TVal&gt;),
    /// and wildcard form (Foo&lt;*,*&gt;).
    /// </summary>
    public (TypeVersionGroup? Group, string? Error) FindExactType(
        ICachedSolution solution,
        string fullTypeName)
    {
        var pattern = wildcardFactory.CreateForTypeNames(TypeWildcardPatternBuilder.Build(fullTypeName));

        var matches = typeProvider.GetAll(solution, versionedStrategy)
            .Where(g => pattern.IsMatch(g.FullName))
            .ToList();

        return matches.Count switch
        {
            0 => (null, $"Type '{fullTypeName}' not found."),
            1 => (matches[0], null),
            _ => (null, $"Ambiguous: '{fullTypeName}' matched multiple types:\n" +
                        string.Join("\n", matches.Select(m => $"  {m.FullName}")))
        };
    }
}
