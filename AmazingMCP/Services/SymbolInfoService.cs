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

        var constants = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.IsConst
                        && f.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .ToList();

        if (constants.Count > 0)
        {
            sb.AppendLine($"{prefix}Constants:");
            foreach (var c in constants)
            {
                var vis = c.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                sb.AppendLine($"{prefix}  {vis}{c.Type.ToDisplayString()} {c.Name} = {FormatConstantValue(c.ConstantValue)}");
            }
        }

        var staticFields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.IsStatic && !f.IsConst
                        && f.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .ToList();

        if (staticFields.Count > 0)
        {
            sb.AppendLine($"{prefix}Static fields:");
            foreach (var f in staticFields)
            {
                var vis = f.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                var ro = f.IsReadOnly ? "readonly " : "";
                sb.AppendLine($"{prefix}  static {vis}{ro}{f.Type.ToDisplayString()} {f.Name}");
            }
        }

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .ToList();

        var staticProps = properties.Where(p => p.IsStatic).ToList();
        var instanceProps = properties.Where(p => !p.IsStatic).ToList();

        if (staticProps.Count > 0)
        {
            sb.AppendLine($"{prefix}Static properties:");
            foreach (var p in staticProps)
            {
                var vis = p.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                var accessors = FormatAccessors(p);
                sb.AppendLine($"{prefix}  static {vis}{p.Type.ToDisplayString()} {p.Name} {{ {accessors} }}");
            }
        }

        if (instanceProps.Count > 0)
        {
            sb.AppendLine($"{prefix}Properties:");
            foreach (var p in instanceProps)
            {
                var vis = p.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                var accessors = FormatAccessors(p);
                sb.AppendLine($"{prefix}  {vis}{p.Type.ToDisplayString()} {p.Name} {{ {accessors} }}");
            }
        }

        var methods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                        && m.MethodKind == MethodKind.Ordinary)
            .ToList();

        var staticMethods = methods.Where(m => m.IsStatic).ToList();
        var instanceMethods = methods.Where(m => !m.IsStatic).ToList();

        if (staticMethods.Count > 0)
        {
            sb.AppendLine($"{prefix}Static methods:");
            foreach (var m in staticMethods)
            {
                var vis = m.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                var parameters = string.Join(", ",
                    m.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                sb.AppendLine($"{prefix}  static {vis}{m.ReturnType.ToDisplayString()} {m.Name}({parameters})");
            }
        }

        if (instanceMethods.Count > 0)
        {
            sb.AppendLine($"{prefix}Methods:");
            foreach (var m in instanceMethods)
            {
                var vis = m.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                var parameters = string.Join(", ",
                    m.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                sb.AppendLine($"{prefix}  {vis}{m.ReturnType.ToDisplayString()} {m.Name}({parameters})");
            }
        }

        var nestedTypes = type.GetTypeMembers()
            .Where(t => t.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .ToList();

        if (nestedTypes.Count > 0)
        {
            sb.AppendLine($"{prefix}Nested types:");
            foreach (var nested in nestedTypes)
            {
                var vis = nested.DeclaredAccessibility == Accessibility.Internal ? "internal " : "";
                sb.AppendLine($"{prefix}  {vis}[{nested.TypeKind}] {nested.ToDisplayString()}");
            }
        }
    }

    static string FormatConstantValue(object? value) =>
        value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            _ => value.ToString() ?? "null"
        };

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
                case INamedTypeSymbol type:
                    if (type.ToDisplayString().Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
                        return type;

                    var nested = FindNestedType(type, fullTypeName);
                    if (nested is not null) return nested;
                    break;

                case INamespaceSymbol childNs:
                    var found = FindType(childNs, fullTypeName);
                    if (found is not null) return found;
                    break;
            }
        }

        return null;
    }

    static INamedTypeSymbol? FindNestedType(INamedTypeSymbol parent, string fullTypeName)
    {
        foreach (var nested in parent.GetTypeMembers())
        {
            if (nested.ToDisplayString().Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
                return nested;

            var deeper = FindNestedType(nested, fullTypeName);
            if (deeper is not null) return deeper;
        }

        return null;
    }
}
