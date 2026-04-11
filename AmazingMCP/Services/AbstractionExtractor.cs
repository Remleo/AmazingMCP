using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class AbstractionExtractor : IAbstractionExtractor
{
    public AbstractionInfo BuildAbstractionInfo(
        RawTypeInfo typeInfo,
        string projectName,
        IReadOnlyList<string> implementations)
    {
        return new AbstractionInfo(
            FullName: typeInfo.FullName,
            Namespace: typeInfo.Namespace,
            ProjectName: projectName,
            SourceFilePath: typeInfo.SourceFilePath,
            IsInterface: typeInfo.IsInterface,
            IsAbstractClass: typeInfo.IsAbstractClass,
            IsStaticClass: typeInfo.IsStaticClass,
            Implementations: implementations,
            OpenGenericFullName: typeInfo.OpenGenericFullName);
    }

    public AbstractionInfo BuildAbstractionInfo(
        INamedTypeSymbol symbol,
        string projectName,
        IReadOnlyList<string> implementations)
    {
        return BuildAbstractionInfo(RawTypeInfo.From(symbol), projectName, implementations);
    }

    public INamedTypeSymbol? FindClosedGenericInterface(string ifaceName, List<SourceType> classes)
    {
        foreach (var entry in classes)
            foreach (var iface in entry.Symbol.AllInterfaces)
                if (iface.ToDisplayString() == ifaceName)
                    return iface;
        return null;
    }

    public string ResolveProjectForClosedGeneric(
        INamedTypeSymbol closedGenericSymbol, List<SourceType> allTypes)
    {
        var originalDefName = closedGenericSymbol.OriginalDefinition.ToDisplayString();

        return allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Interface
                        && t.Symbol.ToDisplayString() == originalDefName)
            .OrderBy(t =>
            {
                var path = t.Symbol.DeclaringSyntaxReferences
                    .FirstOrDefault()?.SyntaxTree.FilePath ?? "";
                return path.Contains(t.ProjectName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            })
            .FirstOrDefault()?.ProjectName ?? "";
    }
}
