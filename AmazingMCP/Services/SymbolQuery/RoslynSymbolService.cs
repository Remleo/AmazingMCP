using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery.Strategies;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace AmazingMCP.Services.SymbolQuery;

public class RoslynSymbolService(
    IWorkspaceProvider workspaceProvider,
    IWildcardPatternFactory wildcardFactory,
    IRoslynTypeProvider typeProvider,
    [FromKeyedServices(TypeEnumerationMode.Versioned)] ITypeEnumerationStrategy<TypeVersionGroup> versionedStrategy)
    : IRoslynSymbolService
{
    public async Task<IReadOnlyList<SymbolResult>> QuerySymbolsAsync(
        string solutionPath,
        IReadOnlyList<string>? typeQuery,
        IReadOnlyList<string>? typeExclude,
        IReadOnlyList<string>? memberQuery,
        CancellationToken ct = default)
    {
        ValidateQuery(typeQuery, memberQuery);

        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);

        var typePatterns = CreatePatterns(typeQuery);
        var typeExcludePatterns = CreatePatterns(typeExclude);
        var memberPatterns = CreatePatterns(memberQuery);

        var seen = new HashSet<SeenSymbolKey>();
        var results = new List<SymbolResult>();

        foreach (var group in typeProvider.GetAll(solution, versionedStrategy))
        {
            if (!IsTypeSelected(group.Best, typePatterns, typeExcludePatterns))
                continue;

            var allVersions = group.Versions.Select(v => v.Version).ToList();

            if (memberPatterns.Count == 0)
                SymbolWalker.CollectType(group.Best, seen, results, allVersions);
            else
                SymbolWalker.CollectMembers(group.Best, memberPatterns, seen, results, allVersions);
        }

        return results;
    }

    /// <inheritdoc />
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

    IReadOnlyList<IWildcardPattern> CreatePatterns(IReadOnlyList<string>? queries)
    {
        if (queries is null)
            return [];

        return [.. queries.Select(CreatePattern)];
    }

    IWildcardPattern CreatePattern(string query)
    {
        // If the query has no wildcards, wrap it as a contains-pattern: *query*
        var wildcardQuery = query.Contains('*') ? query : $"*{query}*";
        return wildcardFactory.CreateForTypeNames(wildcardQuery);
    }

    static void ValidateQuery(IReadOnlyList<string>? typeQuery, IReadOnlyList<string>? memberQuery)
    {
        if (typeQuery is not { Count: > 0 } && memberQuery is not { Count: > 0 })
            throw new ArgumentException("At least one of typeQuery or memberQuery must be specified.");
    }

    static bool IsTypeSelected(
        INamedTypeSymbol type,
        IReadOnlyList<IWildcardPattern> typePatterns,
        IReadOnlyList<IWildcardPattern> typeExcludePatterns)
    {
        if (typePatterns.Count > 0 && !IsTypeMatch(type, typePatterns))
            return false;

        var isExcluded = typeExcludePatterns.Count > 0 && IsTypeMatch(type, typeExcludePatterns);
        return !isExcluded;
    }

    static bool IsTypeMatch(INamedTypeSymbol type, IReadOnlyList<IWildcardPattern> patterns)
    {
        var fullName = type.ToDisplayString();
        return patterns.Any(p => p.IsMatch(fullName) || p.IsMatch(type.Name));
    }
}
