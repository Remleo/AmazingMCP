using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class TypeCollector : ITypeCollector
{
    static readonly HashSet<string> ExcludedInterfaceNames =
    [
        "System.IDisposable",
        "System.IAsyncDisposable",
        "System.ICloneable",
        "System.IComparable",
        "System.IFormattable",
        "System.IConvertible",
        "System.IEquatable",
        "System.IObservable",
        "System.IObserver",
        "System.IServiceProvider"
    ];

    static readonly HashSet<string> ExcludedInterfacePrefixes =
    [
        "System.Collections.",
        "System.Collections.Generic.",
        "System.Threading.",
        "System.Runtime.",
        "System.ComponentModel."
    ];

    public List<SourceType> CollectSourceTypes(
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations)
    {
        var result = new List<SourceType>();
        foreach (var (projectName, compilation) in compilations)
            CollectFromNamespace(compilation.GlobalNamespace, projectName, compilation, result);
        return result;
    }

    public bool IsExcludedInterface(string fullName)
    {
        var nameWithoutGenerics = fullName;
        var idx = fullName.IndexOf('<');
        if (idx >= 0) nameWithoutGenerics = fullName[..idx];

        if (ExcludedInterfaceNames.Contains(nameWithoutGenerics))
            return true;

        foreach (var prefix in ExcludedInterfacePrefixes)
        {
            if (fullName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
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
                case INamedTypeSymbol type when type.DeclaringSyntaxReferences.Length > 0
                    && type.DeclaringSyntaxReferences.Any(r => compilation.ContainsSyntaxTree(r.SyntaxTree)):
                    result.Add(new SourceType(type, projectName, compilation));
                    break;
                case INamespaceSymbol childNs:
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
            if (!IsExcludedInterface(ifaceName) && !result.Contains(ifaceName))
                result.Add(ifaceName);

            // Also collect base interfaces (e.g. IMessageHandler from IMessageHandler<T>)
            CollectAbstractionsFromHierarchy(iface, result, visited);
        }

        if (type.BaseType is not null && type.BaseType.SpecialType == SpecialType.None)
            CollectAbstractionsFromHierarchy(type.BaseType, result, visited);
    }
}
