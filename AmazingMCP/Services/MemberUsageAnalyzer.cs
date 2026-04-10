using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public class MemberUsageAnalyzer : IMemberUsageAnalyzer
{
    public async Task<Dictionary<string, List<MemberUsage>>> AnalyzeUsagesAsync(
        INamedTypeSymbol cls,
        List<ConstructorDependency> ctorDeps,
        Compilation compilation,
        CancellationToken ct)
    {
        // Build per-dep symbol map: dep type full name → INamedTypeSymbol
        var depSymbols = new Dictionary<string, INamedTypeSymbol>();
        foreach (var dep in ctorDeps)
        {
            var symbol = FindTypeByName(compilation, dep.TypeFullName);
            if (symbol is not null)
                depSymbols[dep.TypeFullName] = symbol;
        }

        if (depSymbols.Count == 0)
            return [];

        // usages per dep type full name
        var result = new Dictionary<string, HashSet<MemberUsage>>();
        foreach (var key in depSymbols.Keys)
            result[key] = [];

        var visited = new HashSet<string>();
        await CollectUsagesFromHierarchy(cls, depSymbols, compilation, result, visited, ct);

        return result
            .Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
    }

    async Task CollectUsagesFromHierarchy(
        INamedTypeSymbol cls,
        Dictionary<string, INamedTypeSymbol> depSymbols,
        Compilation compilation,
        Dictionary<string, HashSet<MemberUsage>> result,
        HashSet<string> visited,
        CancellationToken ct)
    {
        if (!visited.Add(cls.ToDisplayString())) return;

        foreach (var syntaxRef in cls.DeclaringSyntaxReferences)
        {
            if (!compilation.ContainsSyntaxTree(syntaxRef.SyntaxTree))
                continue;

            var syntax = await syntaxRef.GetSyntaxAsync(ct);
            var model = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

            foreach (var node in syntax.DescendantNodes())
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation:
                        AnalyzeInvocation(invocation, model, depSymbols, result);
                        break;
                    case MemberAccessExpressionSyntax memberAccess
                        when node.Parent is not InvocationExpressionSyntax:
                        AnalyzeMemberAccess(memberAccess, model, depSymbols, result);
                        break;
                    case AssignmentExpressionSyntax assignment:
                        AnalyzeAssignment(assignment, model, depSymbols, result);
                        break;
                }
            }
        }

        if (cls.BaseType is not null && cls.BaseType.SpecialType == SpecialType.None)
            await CollectUsagesFromHierarchy(cls.BaseType, depSymbols, compilation, result, visited, ct);
    }

    static void AnalyzeInvocation(
        InvocationExpressionSyntax invocation, SemanticModel model,
        Dictionary<string, INamedTypeSymbol> depSymbols,
        Dictionary<string, HashSet<MemberUsage>> result)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { ContainingType: { } containingType } method)
            return;

        var depKey = FindMatchingDep(containingType, depSymbols);
        if (depKey is not null)
            result[depKey].Add(new MemberUsage(method.Name, MemberUsageKind.MethodCall));
    }

    static void AnalyzeMemberAccess(
        MemberAccessExpressionSyntax memberAccess, SemanticModel model,
        Dictionary<string, INamedTypeSymbol> depSymbols,
        Dictionary<string, HashSet<MemberUsage>> result)
    {
        if (model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol { ContainingType: { } containingType } prop)
            return;

        var depKey = FindMatchingDep(containingType, depSymbols);
        if (depKey is not null)
            result[depKey].Add(new MemberUsage(prop.Name, MemberUsageKind.PropertyGet));
    }

    static void AnalyzeAssignment(
        AssignmentExpressionSyntax assignment, SemanticModel model,
        Dictionary<string, INamedTypeSymbol> depSymbols,
        Dictionary<string, HashSet<MemberUsage>> result)
    {
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess) return;
        if (model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol { ContainingType: { } containingType } prop)
            return;

        var depKey = FindMatchingDep(containingType, depSymbols);
        if (depKey is not null)
            result[depKey].Add(new MemberUsage(prop.Name, MemberUsageKind.PropertySet));
    }

    static string? FindMatchingDep(
        INamedTypeSymbol candidateType,
        Dictionary<string, INamedTypeSymbol> depSymbols)
    {
        foreach (var (key, depType) in depSymbols)
        {
            if (SymbolEqualityComparer.Default.Equals(candidateType, depType))
                return key;

            if (depType.TypeKind == TypeKind.Interface)
            {
                foreach (var iface in candidateType.AllInterfaces)
                {
                    if (SymbolEqualityComparer.Default.Equals(iface, depType))
                        return key;
                }
            }
        }

        return null;
    }

    static INamedTypeSymbol? FindTypeByName(Compilation compilation, string fullTypeName)
    {
        var found = FindTypeInNamespace(compilation.GlobalNamespace, fullTypeName)
                    ?? compilation.GetTypeByMetadataName(fullTypeName);
        if (found is not null) return found;

        return FindClosedGenericInCompilation(compilation.GlobalNamespace, fullTypeName);
    }

    static INamedTypeSymbol? FindClosedGenericInCompilation(INamespaceSymbol ns, string fullTypeName)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type when type.DeclaringSyntaxReferences.Length > 0:
                    foreach (var iface in type.AllInterfaces)
                    {
                        if (iface.ToDisplayString().Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
                            return iface;
                    }

                    foreach (var ctor in type.Constructors)
                    {
                        foreach (var param in ctor.Parameters)
                        {
                            if (param.Type is INamedTypeSymbol paramType &&
                                paramType.ToDisplayString().Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
                                return paramType;
                        }
                    }

                    break;
                case INamespaceSymbol childNs:
                    var result = FindClosedGenericInCompilation(childNs, fullTypeName);
                    if (result is not null) return result;
                    break;
            }
        }

        return null;
    }

    static INamedTypeSymbol? FindTypeInNamespace(INamespaceSymbol ns, string fullTypeName)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type
                    when type.ToDisplayString().Equals(fullTypeName, StringComparison.OrdinalIgnoreCase):
                    return type;
                case INamespaceSymbol childNs:
                    var found = FindTypeInNamespace(childNs, fullTypeName);
                    if (found is not null) return found;
                    break;
            }
        }

        return null;
    }
}
