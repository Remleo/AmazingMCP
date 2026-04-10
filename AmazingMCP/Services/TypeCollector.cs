using AmazingMCP.Models;
using AmazingMCP.Services.Scanning;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class TypeCollector(ITypeFilter typeFilter) : ITypeCollector
{
    static readonly HashSet<string> ExcludedNamespaces =
    [
        "AspNetCoreGeneratedDocument" // Razor auto-generated code
    ];
    public List<SourceType> CollectSourceTypes(
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations)
    {
        var result = new List<SourceType>();
        foreach (var (projectName, compilation) in compilations)
            CollectFromNamespace(compilation.GlobalNamespace, projectName, compilation, result);
        return result;
    }

    public List<string> GetAllImplementedAbstractions(INamedTypeSymbol cls)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        CollectAbstractionsFromHierarchy(cls, result, visited);
        return result;
    }

    public List<string> GetBaseClassChain(INamedTypeSymbol cls)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();
        var current = cls.BaseType;

        while (current is not null && current.SpecialType == SpecialType.None)
        {
            var name = current.ToDisplayString();
            if (!visited.Add(name)) break;
            chain.Add(name);
            current = current.BaseType;
        }

        return chain;
    }

    static void CollectFromNamespace(
        INamespaceSymbol ns, string projectName, Compilation compilation, List<SourceType> result)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type
                    when type.DeclaringSyntaxReferences.Length > 0
                    && type.DeclaringSyntaxReferences.Any(r => compilation.ContainsSyntaxTree(r.SyntaxTree)):
                    result.Add(new SourceType(type, projectName, compilation));
                    break;
                case INamespaceSymbol childNs:
                    if (ExcludedNamespaces.Contains(childNs.Name)) break;
                    CollectFromNamespace(childNs, projectName, compilation, result);
                    break;
            }
        }
    }

    void CollectAbstractionsFromHierarchy(
        INamedTypeSymbol type, List<string> result, HashSet<string> visited)
    {
        if (!visited.Add(type.ToDisplayString())) return;

        foreach (var iface in type.Interfaces)
        {
            var ifaceName = iface.ToDisplayString();
            if (!typeFilter.ShouldExcludeByName(ifaceName) && !result.Contains(ifaceName))
                result.Add(ifaceName);
            CollectAbstractionsFromHierarchy(iface, result, visited);
        }

        if (type.BaseType is not null && type.BaseType.SpecialType == SpecialType.None)
            CollectAbstractionsFromHierarchy(type.BaseType, result, visited);
    }
}
