using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class AbstractionExtractor : IAbstractionExtractor
{
    public AbstractionInfo BuildAbstractionInfo(
        INamedTypeSymbol symbol,
        string projectName,
        Dictionary<string, List<string>> implementors)
    {
        var fullName = symbol.ToDisplayString();
        return new AbstractionInfo(
            FullName: fullName,
            Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
            ProjectName: projectName,
            SourceFilePath: GetSourcePath(symbol),
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            DeclaredMembers: GetDeclaredMembers(symbol),
            Implementations: implementors.GetValueOrDefault(fullName, []));
    }

    public INamedTypeSymbol? FindClosedGenericInterface(
        string ifaceName, List<SourceType> classes)
    {
        foreach (var entry in classes)
        {
            foreach (var iface in entry.Symbol.AllInterfaces)
            {
                if (iface.ToDisplayString() == ifaceName)
                    return iface;
            }
        }

        return null;
    }

    public string ResolveProjectForClosedGeneric(
        INamedTypeSymbol closedGenericSymbol, List<SourceType> allTypes)
    {
        var originalDefName = closedGenericSymbol.OriginalDefinition.ToDisplayString();

        var sourceProject = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Interface
                        && t.Symbol.ToDisplayString() == originalDefName)
            .OrderBy(t =>
            {
                var path = GetSourcePath(t.Symbol) ?? "";
                return path.Contains(t.ProjectName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            })
            .FirstOrDefault();

        return sourceProject?.ProjectName ?? "";
    }

    public List<string> GetDeclaredMembers(INamedTypeSymbol symbol)
    {
        var members = new List<string>();

        foreach (var member in symbol.GetMembers())
        {
            if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                continue;

            switch (member)
            {
                case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                    members.Add($"{method.Name}()");
                    break;
                case IPropertySymbol prop:
                    var accessors = new List<string>();
                    if (prop.GetMethod is not null) accessors.Add("get");
                    if (prop.SetMethod is not null) accessors.Add("set");
                    members.Add($"{prop.Name} {{ {string.Join("; ", accessors)}; }}");
                    break;
            }
        }

        return members;
    }

    static string? GetSourcePath(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
}
