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

        var seen = new HashSet<(string FullName, string Assembly)>();
        var results = new List<SymbolResult>();

        foreach (var (_, compilation) in solution.Compilations)
        {
            foreach (var symbol in FindNamedTypes(compilation.GlobalNamespace, pattern))
            {
                var key = (symbol.ToDisplayString(), symbol.ContainingAssembly?.Name ?? "unknown");
                if (seen.Add(key))
                    results.Add(ToResult(symbol));
            }
        }

        return results;
    }

    static IEnumerable<INamedTypeSymbol> FindNamedTypes(INamespaceSymbol ns, IWildcardPattern pattern)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type:
                    if (pattern.IsMatch(type.ToDisplayString()))
                        yield return type;

                    foreach (var nested in FindNestedTypes(type, pattern))
                        yield return nested;
                    break;

                case INamespaceSymbol childNs:
                    foreach (var t in FindNamedTypes(childNs, pattern))
                        yield return t;
                    break;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> FindNestedTypes(INamedTypeSymbol parent, IWildcardPattern pattern)
    {
        foreach (var nested in parent.GetTypeMembers())
        {
            if (nested.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                continue;

            if (pattern.IsMatch(nested.ToDisplayString()))
                yield return nested;

            foreach (var deeper in FindNestedTypes(nested, pattern))
                yield return deeper;
        }
    }

    /// <summary>
    /// Finds a single type by its fully-qualified name across all compilations.
    /// Supports CLR metadata notation (Foo`2), C# generic syntax (Foo&lt;T, TVal&gt;),
    /// and wildcard form (Foo&lt;*,*&gt;). Returns null if not found or ambiguous (multiple matches).
    /// </summary>
    public async Task<(INamedTypeSymbol? Symbol, string? Error)> FindExactTypeAsync(
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
            foreach (var symbol in FindNamedTypes(compilation.GlobalNamespace, pattern))
            {
                if (seen.Add(symbol.ToDisplayString()))
                    matches.Add(symbol);
            }
        }

        return matches.Count switch
        {
            0 => (null, $"Type '{fullTypeName}' not found."),
            1 => (matches[0], null),
            _ => (null, $"Ambiguous: '{fullTypeName}' matched multiple types:\n" +
                        string.Join("\n", matches.Select(m => $"  {m.ToDisplayString()}")))
        };
    }

    static SymbolResult ToResult(INamedTypeSymbol symbol)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        string? sourceFilePath = null;
        int? definitionLine = null;

        if (syntaxRef is not null)
        {
            sourceFilePath = syntaxRef.SyntaxTree.FilePath;
            definitionLine = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span).StartLinePosition.Line + 1;
        }

        return new(
            Name: symbol.Name,
            FullName: symbol.ToDisplayString(),
            Kind: symbol.TypeKind.ToString(),
            ContainingAssembly: symbol.ContainingAssembly?.Name,
            SourceFilePath: sourceFilePath,
            DefinitionLine: definitionLine);
    }
}