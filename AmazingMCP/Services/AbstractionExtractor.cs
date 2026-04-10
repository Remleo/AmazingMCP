using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class AbstractionExtractor : IAbstractionExtractor
{
    public AbstractionInfo BuildAbstractionInfo(
        INamedTypeSymbol symbol,
        string projectName,
        IReadOnlyList<string> implementations)
    {
        return new AbstractionInfo(
            FullName: symbol.ToDisplayString(),
            Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
            ProjectName: projectName,
            SourceFilePath: GetSourcePath(symbol),
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            IsAbstractClass: symbol.TypeKind == TypeKind.Class && symbol.IsAbstract,
            IsStaticClass: symbol.TypeKind == TypeKind.Class && symbol.IsStatic,
            Implementations: implementations);
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
                var path = GetSourcePath(t.Symbol) ?? "";
                return path.Contains(t.ProjectName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            })
            .FirstOrDefault()?.ProjectName ?? "";
    }

    static string? GetSourcePath(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
}
