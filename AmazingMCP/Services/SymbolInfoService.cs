using System.Text;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class SymbolInfoService(IWorkspaceProvider workspaceProvider)
{
    static readonly string[] SkippedPrefixes = ["System.", "Microsoft."];

    public async Task<string> GetSymbolInfoAsync(
        string solutionPath,
        string fullTypeName,
        CancellationToken ct = default)
    {
        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);

        INamedTypeSymbol? found = null;

        foreach (var (_, compilation) in solution.Compilations)
        {
            found = FindType(compilation.GlobalNamespace, fullTypeName);
            if (found is not null) break;
        }

        if (found is null)
            return $"Type '{fullTypeName}' not found.";

        var sb = new StringBuilder();
        var visited = new HashSet<string>();
        Describe(found, sb, indent: 0, visited);
        return sb.ToString();
    }

    static void Describe(INamedTypeSymbol type, StringBuilder sb, int indent, HashSet<string> visited)
    {
        var prefix = new string(' ', indent);
        var fullName = type.ToDisplayString();

        if (!visited.Add(fullName))
        {
            sb.AppendLine($"{prefix}(see {fullName} above)");
            return;
        }

        var syntaxRef = type.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is not null)
        {
            var line = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span).StartLinePosition.Line + 1;
            sb.AppendLine($"{prefix}[{type.TypeKind}] {fullName}  (source: {syntaxRef.SyntaxTree.FilePath}, line {line})");
        }
        else
        {
            sb.AppendLine($"{prefix}[{type.TypeKind}] {fullName}  (assembly: {type.ContainingAssembly?.Name})");

        }

        if (type.TypeKind == TypeKind.Enum)
        {
            DescribeEnum(type, sb, indent + 2);
            return;
        }

        DescribeMembers(type, sb, indent + 2);
        DescribeHierarchy(type, sb, indent + 2, visited);
    }

    static void DescribeEnum(INamedTypeSymbol type, StringBuilder sb, int indent)
    {
        var prefix = new string(' ', indent);
        var underlyingType = type.EnumUnderlyingType?.ToDisplayString() ?? "int";
        sb.AppendLine($"{prefix}Underlying type: {underlyingType}");
        sb.AppendLine($"{prefix}Values:");

        foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue)
                sb.AppendLine($"{prefix}  {member.Name} = {member.ConstantValue}");
        }
    }

    static void DescribeMembers(INamedTypeSymbol type, StringBuilder sb, int indent)
    {
        var prefix = new string(' ', indent);

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        if (properties.Count > 0)
        {
            sb.AppendLine($"{prefix}Properties:");
            foreach (var p in properties)
            {
                var accessors = FormatAccessors(p);
                sb.AppendLine($"{prefix}  {p.Type.ToDisplayString()} {p.Name} {{ {accessors} }}");
            }
        }

        var methods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public
                        && m.MethodKind == MethodKind.Ordinary)
            .ToList();

        if (methods.Count > 0)
        {
            sb.AppendLine($"{prefix}Methods:");
            foreach (var m in methods)
            {
                var parameters = string.Join(", ",
                    m.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                sb.AppendLine($"{prefix}  {m.ReturnType.ToDisplayString()} {m.Name}({parameters})");
            }
        }
    }

    static bool IsWellKnownFrameworkType(INamedTypeSymbol type)
    {
        var name = type.ToDisplayString();
        foreach (var p in SkippedPrefixes)
            if (name.StartsWith(p, StringComparison.Ordinal))
                return true;
        return false;
    }

    static void DescribeHierarchy(
        INamedTypeSymbol type, StringBuilder sb, int indent, HashSet<string> visited)
    {
        var prefix = new string(' ', indent);

        if (type.BaseType is not null
            && type.BaseType.SpecialType == SpecialType.None) // skip System.Object etc.
        {
            if (IsWellKnownFrameworkType(type.BaseType))
            {
                sb.AppendLine($"{prefix}Base type: {type.BaseType.ToDisplayString()} (skipped — well-known framework type)");
            }
            else
            {
                sb.AppendLine($"{prefix}Base type:");
                Describe(type.BaseType, sb, indent + 2, visited);
            }
        }

        var interfaces = type.Interfaces;
        if (interfaces.Length > 0)
        {
            var toDescribe = new List<INamedTypeSymbol>();
            var skipped = new List<string>();

            foreach (var iface in interfaces)
            {
                if (IsWellKnownFrameworkType(iface))
                    skipped.Add(iface.ToDisplayString());
                else
                    toDescribe.Add(iface);
            }

            if (toDescribe.Count > 0)
            {
                sb.AppendLine($"{prefix}Implements:");
                foreach (var iface in toDescribe)
                    Describe(iface, sb, indent + 2, visited);
            }

            if (skipped.Count > 0)
            {
                sb.AppendLine($"{prefix}Implements (skipped — well-known framework types):");
                foreach (var name in skipped)
                    sb.AppendLine($"{prefix}  {name}");
            }
        }
    }

    static string FormatAccessors(IPropertySymbol p)
    {
        var parts = new List<string>();
        if (p.GetMethod is not null) parts.Add("get;");
        if (p.SetMethod is not null) parts.Add(p.SetMethod.IsInitOnly ? "init;" : "set;");
        return string.Join(" ", parts);
    }

    static INamedTypeSymbol? FindType(INamespaceSymbol ns, string fullTypeName)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol type
                    when type.ToDisplayString().Equals(fullTypeName, StringComparison.OrdinalIgnoreCase):
                    return type;

                case INamespaceSymbol childNs:
                    var found = FindType(childNs, fullTypeName);
                    if (found is not null) return found;
                    break;
            }
        }

        return null;
    }
}
