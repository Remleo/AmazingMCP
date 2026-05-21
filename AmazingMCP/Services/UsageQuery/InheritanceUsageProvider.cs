using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Finds all types that inherit from or implement the target type,
/// producing a <see cref="UsageMatch"/> per derived type with <see cref="UsageKind.Inheritance"/>.
/// </summary>
public class InheritanceUsageProvider(
    IInheritanceSearchSymbolResolver symbolResolver,
    IDerivedTypeService derivedTypeService) : IInheritanceUsageProvider
{
    public IReadOnlyList<UsageMatch> FindMatches(
        ICachedSolution cachedSolution,
        string typeName,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? includePatterns,
        List<IWildcardPattern>? excludePatterns)
    {
        var target = symbolResolver.Resolve(cachedSolution, typeName);
        if (target is null)
            return [];

        var derived = derivedTypeService.FindDerivedTypes(cachedSolution, target);
        var results = new List<UsageMatch>(derived.Count);

        foreach (var type in derived)
        {
            var candidateName = type.ToDisplayString();

            if (includePatterns is not null && !includePatterns.Any(p => p.IsMatch(candidateName)))
                continue;
            if (excludePatterns is not null && excludePatterns.Any(p => p.IsMatch(candidateName)))
                continue;

            var entry = new QueryEntry
            {
                Kind = UsageKind.Inheritance,
                TypeName = target.FullName,
            };

            if (predicate is not null && !predicate(entry))
                continue;

            results.Add(BuildMatch(type, entry));
        }

        return results;
    }

    static UsageMatch BuildMatch(INamedTypeSymbol type, QueryEntry entry)
    {
        var typeName = type.ToDisplayString();
        var syntaxRef = type.DeclaringSyntaxReferences.FirstOrDefault();

        if (syntaxRef is not null)
        {
            var node = syntaxRef.GetSyntax();

            var declarationRange = node switch
            {
                TypeDeclarationSyntax typeDecl => DeclarationRangeResolver.Resolve(typeDecl),
                EnumDeclarationSyntax enumDecl => DeclarationRangeResolver.Resolve(enumDecl),
                _ => DeclarationRangeResolver.ResolveFallback(node),
            };

            var scope = new UsageScope(
                TypeName: typeName,
                FilePath: syntaxRef.SyntaxTree.FilePath,
                MethodName: null,
                MethodDefinitionRange: null,
                MethodFullRange: null,
                Section: new ScopeSection(node, declarationRange.Start, declarationRange.End),
                MatchLine: declarationRange.Start);

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
        return $"// assembly: {assemblyName}\n{TypeDeclarationFormatter.FormatHeader(type, includeInheritance: true)}";
    }
}
