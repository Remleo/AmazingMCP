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
            CollectMatchingTypes(compilation.GlobalNamespace, pattern, seen, results);
            CollectMatchingMembers(compilation.GlobalNamespace, pattern, seen, results);
        }

        return results;
    }

    /// <summary>
    /// Finds a single type by its fully-qualified name across all compilations.
    /// Supports CLR metadata notation (Foo`2), C# generic syntax (Foo&lt;T, TVal&gt;),
    /// and wildcard form (Foo&lt;*,*&gt;). Returns null if not found or ambiguous (multiple matches).
    /// </summary>
    public async Task<(INamedTypeSymbol? Symbol, string? Error, CachedSolution Solution)> FindExactTypeAsync(
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

    // ── Collection helpers ────────────────────────────────────────────────────

    static void CollectMatchingTypes(
        INamespaceSymbol ns,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        foreach (var typeSymbol in RoslynTypeEnumerator.FindNamedTypes(ns, pattern))
        {
            var key = SeenSymbolKey.ForType(
                typeDisplayName: typeSymbol.ToDisplayString(),
                assembly: typeSymbol.ContainingAssembly?.Name ?? "unknown");
            if (seen.Add(key))
                results.Add(ToTypeResult(typeSymbol));
        }
    }

    static void CollectMatchingMembers(
        INamespaceSymbol ns,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        foreach (var typeSymbol in RoslynTypeEnumerator.EnumerateAll(ns))
        {
            if (!IsMemberSearchCandidate(typeSymbol))
                continue;

            var declaringType = ToTypeResult(typeSymbol);
            var assembly = typeSymbol.ContainingAssembly?.Name ?? "unknown";

            foreach (var member in typeSymbol.GetMembers())
            {
                if (!IsVisibleMember(member))
                    continue;

                if (member is IMethodSymbol method)
                    TryAddMethod(method, declaringType, assembly, pattern, seen, results);
                else if (member is IPropertySymbol property)
                    TryAddProperty(property, declaringType, assembly, pattern, seen, results);
            }
        }
    }

    static void TryAddMethod(
        IMethodSymbol method,
        SymbolResult declaringType,
        string assembly,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (method.MethodKind != MethodKind.Ordinary) return;
        if (!pattern.IsMatch(method.Name)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: method.ContainingType.ToDisplayString(),
            memberSignature: MemberFullName(method),
            assembly: assembly);
        if (seen.Add(key))
            results.Add(ToMethodResult(method, declaringType));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        SymbolResult declaringType,
        string assembly,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!pattern.IsMatch(property.Name)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: property.ContainingType.ToDisplayString(),
            memberSignature: MemberFullName(property),
            assembly: assembly);
        if (seen.Add(key))
            results.Add(ToPropertyResult(property, declaringType));
    }

    // ── Candidate filtering ───────────────────────────────────────────────────

    /// Only search members on classes and interfaces; skip well-known framework types.
    static bool IsMemberSearchCandidate(INamedTypeSymbol type) =>
        (type.TypeKind is TypeKind.Class or TypeKind.Interface) &&
        !WellKnownFrameworkTypes.IsWellKnown(type);

    static bool IsVisibleMember(ISymbol member) =>
        member.DeclaredAccessibility is
            Accessibility.Public or
            Accessibility.Internal or
            Accessibility.Protected or
            Accessibility.ProtectedOrInternal;

    // ── Result builders ───────────────────────────────────────────────────────

    static SymbolResult ToTypeResult(INamedTypeSymbol symbol)
    {
        var (path, line) = GetSourceLocation(symbol.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = symbol.Name,
            FullName = symbol.ToDisplayString(),
            Kind = symbol.TypeKind.ToString(),
            ContainingAssembly = symbol.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
        };
    }

    static SymbolResult ToMethodResult(IMethodSymbol method, SymbolResult declaringType)
    {
        var (path, line) = GetSourceLocation(method.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = method.Name,
            FullName = MemberFullName(method),
            Kind = "Method",
            ContainingAssembly = method.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
            DeclaringType = declaringType,
        };
    }

    static SymbolResult ToPropertyResult(IPropertySymbol property, SymbolResult declaringType)
    {
        var (path, line) = GetSourceLocation(property.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = property.Name,
            FullName = MemberFullName(property),
            Kind = "Property",
            ContainingAssembly = property.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
            DeclaringType = declaringType,
        };
    }

    static (string? Path, int? Line) GetSourceLocation(Microsoft.CodeAnalysis.SyntaxReference? syntaxRef)
    {
        if (syntaxRef is null) return (null, null);
        return (
            syntaxRef.SyntaxTree.FilePath,
            syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span).StartLinePosition.Line + 1);
    }

    static string MemberFullName(IMethodSymbol method) =>
        method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithMemberOptions(
                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeType)
            .WithParameterOptions(
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeName));

    static string MemberFullName(IPropertySymbol property) =>
        property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithMemberOptions(
                SymbolDisplayMemberOptions.IncludeType));
}

