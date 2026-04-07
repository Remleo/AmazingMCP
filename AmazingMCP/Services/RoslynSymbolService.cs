using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class RoslynSymbolService(IWorkspaceProvider workspaceProvider)
{
    public async Task<IReadOnlyList<SymbolResult>> QuerySymbolsAsync(
        string solutionPath,
        string query,
        CancellationToken ct = default)
    {
        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);

        var qualifiedQuery = query.Contains('.');
        var seen = new HashSet<(string FullName, string Assembly)>();
        var results = new List<SymbolResult>();

        foreach (var (_, compilation) in solution.Compilations)
        {
            foreach (var symbol in FindNamedTypes(compilation.GlobalNamespace, query, qualifiedQuery))
            {
                var key = (symbol.ToDisplayString(), symbol.ContainingAssembly?.Name ?? "unknown");
                if (seen.Add(key))
                    results.Add(ToResult(symbol));
            }
        }

        return results;
    }

    static IEnumerable<INamedTypeSymbol> FindNamedTypes(
        INamespaceSymbol ns, string query, bool qualifiedQuery)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type when Matches(type, query, qualifiedQuery):
                    yield return type;
                    break;

                case INamespaceSymbol childNs:
                    foreach (var t in FindNamedTypes(childNs, query, qualifiedQuery))
                        yield return t;
                    break;
            }
        }
    }

    static bool Matches(INamedTypeSymbol type, string query, bool qualifiedQuery) =>
        qualifiedQuery
            ? type.ToDisplayString().Contains(query, StringComparison.OrdinalIgnoreCase)
            : type.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

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
