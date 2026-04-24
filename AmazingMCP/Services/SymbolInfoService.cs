using System.Text;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class SymbolInfoService(RoslynSymbolService roslynSymbolService)
{
    static readonly string[] SkippedPrefixes = ["System.", "Microsoft."];

    // Displays: accessibility + modifiers (abstract/virtual/override/static/readonly/const) + type + name + params + constant value.
    // Does NOT include the containing type name in the output.
    static readonly SymbolDisplayFormat MemberFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMemberOptions(
            SymbolDisplayMemberOptions.IncludeAccessibility |
            SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType |
            SymbolDisplayMemberOptions.IncludeRef |
            SymbolDisplayMemberOptions.IncludeConstantValue)
        .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters)
        .WithParameterOptions(
            SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeDefaultValue);

    public async Task<string> GetSymbolInfoAsync(
        string solutionPath,
        string fullTypeName,
        CancellationToken ct = default)
    {
        var (found, error, cachedSolution) = await roslynSymbolService.FindExactTypeAsync(solutionPath, fullTypeName, ct);

        if (found is null)
            return error!;

        var sb = new StringBuilder();
        var visited = new HashSet<string>();
        Describe(found, sb, indent: 0, visited);
        DescribeDerivedTypes(found, cachedSolution, sb);
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
        var typeHeader = FormatTypeHeader(type);
        if (syntaxRef is not null)
        {
            var line = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span).StartLinePosition.Line + 1;
            sb.AppendLine($"{prefix}{typeHeader}  (source: {syntaxRef.SyntaxTree.FilePath}, line {line})");
        }
        else
        {
            sb.AppendLine($"{prefix}{typeHeader}  (assembly: {type.ContainingAssembly?.Name})");
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

        // Emit members in declaration order, skipping property accessors and invisible members.
        foreach (var member in type.GetMembers())
        {
            if (!IsVisible(member.DeclaredAccessibility))
                continue;

            switch (member)
            {
                case IFieldSymbol f:
                    sb.AppendLine($"{prefix}{f.ToDisplayString(MemberFormat)}");
                    break;

                case IPropertySymbol p:
                    // Skip indexers for now; they appear as IPropertySymbol with IsIndexer == true.
                    sb.AppendLine($"{prefix}{p.ToDisplayString(MemberFormat)} {{ {FormatAccessors(p)} }}");
                    break;

                case IMethodSymbol m when m.MethodKind == MethodKind.Constructor:
                    sb.AppendLine($"{prefix}{m.ToDisplayString(MemberFormat)}");
                    break;

                case IMethodSymbol m when m.MethodKind == MethodKind.Ordinary:
                    sb.AppendLine($"{prefix}{m.ToDisplayString(MemberFormat)}");
                    break;
            }
        }

        // Nested types follow after all other members.
        foreach (var nested in type.GetTypeMembers().Where(t => IsVisible(t.DeclaredAccessibility)))
            sb.AppendLine($"{prefix}{FormatTypeHeader(nested)}");
    }

    // Returns true for all accessibilities visible outside the declaring type (public, internal, protected variants).
    static bool IsVisible(Accessibility a) => a is
        Accessibility.Public or
        Accessibility.Internal or
        Accessibility.Protected or
        Accessibility.ProtectedOrInternal or
        Accessibility.ProtectedAndInternal;

    // Returns the visibility prefix string for all visible members.
    static string FormatVisibility(Accessibility a) => a switch
    {
        Accessibility.Public => "public ",
        Accessibility.Internal => "internal ",
        Accessibility.Protected => "protected ",
        Accessibility.ProtectedOrInternal => "protected internal ",
        Accessibility.ProtectedAndInternal => "private protected ",
        _ => ""
    };

    // Returns the full type declaration header: visibility + modifiers + kind keyword + name.
    // Example: "public abstract class AnimalBase", "internal sealed class Utils", "public interface IAnimal"
    static string FormatTypeHeader(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        sb.Append(FormatVisibility(type.DeclaredAccessibility));

        if (type.IsStatic) sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class) sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class) sb.Append("sealed ");

        sb.Append(type.TypeKind switch
        {
            TypeKind.Interface => "interface ",
            TypeKind.Enum => "enum ",
            TypeKind.Struct => "struct ",
            TypeKind.Delegate => "delegate ",
            _ => "class "
        });

        sb.Append(type.ToDisplayString());
        return sb.ToString();
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
                sb.AppendLine();
                sb.AppendLine($"{prefix}Base type: {type.BaseType.ToDisplayString()} (skipped — well-known framework type)");
            }
            else
            {
                sb.AppendLine();
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
                sb.AppendLine();
                sb.AppendLine($"{prefix}Implements:");
                foreach (var iface in toDescribe)
                    Describe(iface, sb, indent + 2, visited);
            }

            if (skipped.Count > 0)
            {
                sb.AppendLine();
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

    static void DescribeDerivedTypes(INamedTypeSymbol type, CachedSolution cachedSolution, StringBuilder sb)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Interface))
            return;

        var derived = RoslynDerivedTypeService.FindDerivedTypes(cachedSolution, type);
        if (derived.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine(type.TypeKind == TypeKind.Interface
            ? "Known implementors / derived types:"
            : "Known derived types:");

        foreach (var d in derived)
            sb.AppendLine($"  {d.ToDisplayString()}");
    }
}
