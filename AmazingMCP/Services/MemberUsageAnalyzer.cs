using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public class MemberUsageAnalyzer : IMemberUsageAnalyzer
{
    public async Task<List<MemberUsage>> AnalyzeUsagesAsync(
        INamedTypeSymbol cls,
        List<ConstructorDependency> ctorDeps,
        Compilation compilation,
        CancellationToken ct)
    {
        var usages = new HashSet<MemberUsage>();

        var depTypeSymbols = new List<INamedTypeSymbol>();
        foreach (var dep in ctorDeps)
        {
            var depSymbol = FindTypeByName(compilation, dep.TypeFullName);
            if (depSymbol is not null)
                depTypeSymbols.Add(depSymbol);
        }

        if (depTypeSymbols.Count == 0) return [];

        var visited = new HashSet<string>();
        await CollectUsagesFromHierarchy(cls, depTypeSymbols, compilation, usages, visited, ct);
        return usages.ToList();
    }

    async Task CollectUsagesFromHierarchy(
        INamedTypeSymbol cls,
        List<INamedTypeSymbol> depTypeSymbols,
        Compilation compilation,
        HashSet<MemberUsage> usages,
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
                        AnalyzeInvocation(invocation, model, depTypeSymbols, usages);
                        break;
                    case MemberAccessExpressionSyntax memberAccess
                        when node.Parent is not InvocationExpressionSyntax:
                        AnalyzeMemberAccess(memberAccess, model, depTypeSymbols, usages);
                        break;
                    case AssignmentExpressionSyntax assignment:
                        AnalyzeAssignment(assignment, model, depTypeSymbols, usages);
                        break;
                }
            }
        }

        if (cls.BaseType is not null && cls.BaseType.SpecialType == SpecialType.None)
            await CollectUsagesFromHierarchy(cls.BaseType, depTypeSymbols, compilation, usages, visited, ct);
    }

    static void AnalyzeInvocation(
        InvocationExpressionSyntax invocation, SemanticModel model,
        List<INamedTypeSymbol> depTypes, HashSet<MemberUsage> usages)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { ContainingType: { } containingType } method)
            return;
        if (IsDependencyType(containingType, depTypes))
            usages.Add(new MemberUsage(method.Name, MemberUsageKind.MethodCall));
    }

    static void AnalyzeMemberAccess(
        MemberAccessExpressionSyntax memberAccess, SemanticModel model,
        List<INamedTypeSymbol> depTypes, HashSet<MemberUsage> usages)
    {
        if (model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol { ContainingType: { } containingType } prop)
            return;
        if (IsDependencyType(containingType, depTypes))
            usages.Add(new MemberUsage(prop.Name, MemberUsageKind.PropertyGet));
    }

    static void AnalyzeAssignment(
        AssignmentExpressionSyntax assignment, SemanticModel model,
        List<INamedTypeSymbol> depTypes, HashSet<MemberUsage> usages)
    {
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess) return;
        if (model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol { ContainingType: { } containingType } prop)
            return;
        if (IsDependencyType(containingType, depTypes))
            usages.Add(new MemberUsage(prop.Name, MemberUsageKind.PropertySet));
    }

    static bool IsDependencyType(INamedTypeSymbol candidateType, List<INamedTypeSymbol> depTypes)
    {
        foreach (var depType in depTypes)
        {
            if (SymbolEqualityComparer.Default.Equals(candidateType, depType))
                return true;

            if (depType.TypeKind == TypeKind.Interface)
            {
                foreach (var iface in candidateType.AllInterfaces)
                {
                    if (SymbolEqualityComparer.Default.Equals(iface, depType))
                        return true;
                }
            }
        }

        return false;
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
