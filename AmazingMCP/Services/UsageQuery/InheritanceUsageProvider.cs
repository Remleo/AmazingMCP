using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Finds all types that inherit from or implement the target type,
/// producing a <see cref="UsageMatch"/> per derived type with <see cref="UsageKind.TypeAsInheritance"/>.
/// </summary>
static class InheritanceUsageProvider
{
    /// <summary>
    /// Finds the target type by its fully-qualified display name, then returns all inheritance matches.
    /// Returns empty if the type is not found.
    /// </summary>
    public static IReadOnlyList<UsageMatch> FindMatches(
        ICachedSolution cachedSolution,
        string typeName,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? includePatterns,
        List<IWildcardPattern>? excludePatterns)
    {
        var targetType = FindType(cachedSolution, typeName);
        if (targetType is null)
            return [];

        return FindMatches(cachedSolution, targetType, predicate, includePatterns, excludePatterns);
    }

    static INamedTypeSymbol? FindType(ICachedSolution cachedSolution, string typeName) =>
        RoslynTypeEnumerator.EnumerateAll(cachedSolution)
            .FirstOrDefault(t => t.ToDisplayString() == typeName);

    /// <summary>
    /// Returns inheritance matches for all types that derive from or implement <paramref name="targetType"/>.
    /// Source types produce a match pointing to their declaration file.
    /// Third-party types produce a match with a synthetic declaration and an empty file path.
    /// </summary>
    public static IReadOnlyList<UsageMatch> FindMatches(
        ICachedSolution cachedSolution,
        INamedTypeSymbol targetType,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? includePatterns,
        List<IWildcardPattern>? excludePatterns)
    {
        var derived = RoslynDerivedTypeService.FindDerivedTypes(cachedSolution, targetType);
        var results = new List<UsageMatch>(derived.Count);

        foreach (var type in derived)
        {
            var typeName = type.ToDisplayString();

            if (includePatterns is not null && !includePatterns.Any(p => p.IsMatch(typeName)))
                continue;
            if (excludePatterns is not null && excludePatterns.Any(p => p.IsMatch(typeName)))
                continue;

            var entry = new QueryEntry
            {
                Kind = UsageKind.TypeAsInheritance,
                TypeName = targetType.ToDisplayString(),
            };

            if (predicate is not null && !predicate(entry))
                continue;

            var match = BuildMatch(type, entry);
            results.Add(match);
        }

        return results;
    }

    static UsageMatch BuildMatch(INamedTypeSymbol type, QueryEntry entry)
    {
        var typeName = type.ToDisplayString();
        var syntaxRef = type.DeclaringSyntaxReferences.FirstOrDefault();

        if (syntaxRef is not null)
        {
            var span = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span);
            var startLine = span.StartLinePosition.Line + 1;
            var endLine = span.EndLinePosition.Line + 1;
            var node = syntaxRef.GetSyntax();

            var scope = new UsageScope(
                TypeName: typeName,
                FilePath: syntaxRef.SyntaxTree.FilePath,
                MethodName: null,
                MethodDefinitionRange: null,
                MethodFullRange: null,
                Section: new ScopeSection(node, startLine, endLine),
                MatchLine: startLine);

            return new UsageMatch(entry, scope);
        }

        // Third-party type — no source file, produce synthetic declaration
        var synthetic = BuildSyntheticDeclaration(type);

        var syntheticScope = new UsageScope(
            TypeName: typeName,
            FilePath: string.Empty,
            MethodName: null,
            MethodDefinitionRange: null,
            MethodFullRange: null,
            Section: null,
            MatchLine: 0,
            SyntheticDeclaration: synthetic);

        return new UsageMatch(entry, syntheticScope);
    }

    static string BuildSyntheticDeclaration(INamedTypeSymbol type)
    {
        var assemblyName = type.ContainingAssembly?.Name ?? "unknown";
        return $"// assembly: {assemblyName}\n{TypeDeclarationFormatter.FormatHeader(type)}";
    }
}
