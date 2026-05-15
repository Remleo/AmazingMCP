using System.Text;
using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.Wildcard;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

public class SymbolInfoService(RoslynSymbolService roslynSymbolService, IXmlDocExtractor xmlDoc, IWildcardPatternFactory wildcardFactory)
{

    public int CompactModeThreshold { get; set; } = 25;

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
            SymbolDisplayParameterOptions.IncludeExtensionThis |
            SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeDefaultValue);

    public async Task<string> GetSymbolInfoAsync(
        string solutionPath,
        string fullTypeName,
        string[]? memberFilters = null,
        CancellationToken ct = default)
    {
        var (found, error, cachedSolution) = await roslynSymbolService.FindExactTypeAsync(solutionPath, fullTypeName, ct);

        if (found is null)
            return error!;

        var sb = new StringBuilder();
        var visited = new HashSet<string>();
        Describe(found, sb, indent: 0, visited, memberFilters, inheritedCompact: false);
        DescribeDerivedTypes(found, cachedSolution, sb);

        if (memberFilters is { Length: > 0 })
        {
            var filters = string.Join(", ", memberFilters.Select(f => $"\"{f}\""));
            sb.AppendLine();
            sb.AppendLine($"// NOTE: Output is filtered. Only members matching [{filters}] are shown.");
        }

        return sb.ToString();
    }

    void Describe(INamedTypeSymbol type, StringBuilder sb, int indent, HashSet<string> visited, string[]? memberFilters, bool inheritedCompact)
    {
        var prefix = new string(' ', indent);
        var fullName = type.ToDisplayString();

        if (!visited.Add(fullName))
        {
            sb.AppendLine($"{prefix}(see {fullName} above)");
            return;
        }

        var syntaxRef = type.DeclaringSyntaxReferences.FirstOrDefault();
        var nestedPrefix = type.ContainingType is not null ? "/* nested */ " : "";
        var typeHeader = $"{nestedPrefix}{FormatTypeHeader(type)}";
        if (syntaxRef is null)
        {
            var doc = xmlDoc.ExtractSymbolDoc(type, prefix);
            if (doc is not null)
                sb.AppendLine(doc);
        }
        sb.AppendLine($"{prefix}{typeHeader} {FormatTypeLocation(type)}");

        if (type.TypeKind == TypeKind.Enum)
        {
            DescribeEnum(type, sb, indent + 2);
            return;
        }

        var isCompact = IsCompactMode(type, memberFilters) || inheritedCompact;
        DescribeMembers(type, sb, indent + 2, memberFilters, inheritedCompact);
        DescribeHierarchy(type, sb, indent + 2, visited, isCompact, memberFilters);
    }

    static void DescribeEnum(INamedTypeSymbol type, StringBuilder sb, int indent)
    {
        var prefix = new string(' ', indent);

        var underlyingType = type.EnumUnderlyingType?.ToDisplayString()
            ?? "int";

        sb.AppendLine($"{prefix}Underlying type: {underlyingType}");
        sb.AppendLine($"{prefix}Values:");

        foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue)
                sb.AppendLine($"{prefix}  {member.Name} = {member.ConstantValue}");
        }
    }

    void DescribeMembers(INamedTypeSymbol type, StringBuilder sb, int indent, string[]? memberFilters, bool inheritedCompact)
    {
        var prefix = new string(' ', indent);
        var isThirdParty = type.DeclaringSyntaxReferences.IsEmpty;
        var members = FilterMembers(type, memberFilters);
        var isCompact = inheritedCompact || members.Count > CompactModeThreshold;

        if (isCompact)
            AppendCompactMembers(members, sb, prefix, inheritedCompact);
        else
            AppendFullMembers(members, sb, prefix, isThirdParty);

        AppendNestedTypes(type, sb, prefix, isThirdParty);
    }

    List<ISymbol> FilterMembers(INamedTypeSymbol type, string[]? memberFilters)
    {
        var members = CollectVisibleMembers(type);

        if (memberFilters is not { Length: > 0 })
            return members;

        var matchers = memberFilters
            .Select(wildcardFactory.CreateGlob)
            .ToArray();

        return members
            .Where(m => matchers.Any(p => p.IsMatch(m.Name)))
            .ToList();
    }

    bool IsCompactMode(INamedTypeSymbol type, string[]? memberFilters) =>
        FilterMembers(type, memberFilters).Count > CompactModeThreshold;

    static void AppendCompactMembers(List<ISymbol> members, StringBuilder sb, string prefix, bool inheritedCompact)
    {
        if (!inheritedCompact)
        {
            sb.AppendLine($"{prefix}// NOTE: This type has too many members ({members.Count}). Only member names are shown.");
            sb.AppendLine($"{prefix}// To see full signatures, pass memberFilters, e.g.: memberFilters: [\"*Get*\", \"Create*\", \"MemberFullName\"]");
        }

        foreach (var member in members)
            sb.AppendLine($"{prefix}{member.Name}");
    }

    void AppendFullMembers(List<ISymbol> members, StringBuilder sb, string prefix, bool isThirdParty)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case IEventSymbol e:
                    AppendMemberDoc(e, sb, prefix, isThirdParty);
                    sb.AppendLine($"{prefix}{e.ToDisplayString(MemberFormat)}");
                    break;

                case IFieldSymbol f:
                    AppendMemberDoc(f, sb, prefix, isThirdParty);
                    sb.AppendLine($"{prefix}{f.ToDisplayString(MemberFormat)}");
                    break;

                case IPropertySymbol p:
                    AppendMemberDoc(p, sb, prefix, isThirdParty);
                    sb.AppendLine($"{prefix}{p.ToDisplayString(MemberFormat)} {{ {FormatAccessors(p)} }}");
                    break;

                case IMethodSymbol m:
                    AppendMemberDoc(m, sb, prefix, isThirdParty);
                    sb.AppendLine($"{prefix}{m.ToDisplayString(MemberFormat)}");
                    break;
            }
        }
    }

    static void AppendNestedTypes(INamedTypeSymbol type, StringBuilder sb, string prefix, bool isThirdParty)
    {
        var nestedTypes = isThirdParty
            ? type.GetTypeMembers().Where(t => IsVisible(t.DeclaredAccessibility))
            : type.GetTypeMembers();

        foreach (var nested in nestedTypes)
            sb.AppendLine($"{prefix}{FormatTypeHeader(nested)}");
    }

    static List<ISymbol> CollectVisibleMembers(INamedTypeSymbol type)
    {
        var result = new List<ISymbol>();
        foreach (var member in type.GetMembers())
        {
            if (!IsVisible(member.DeclaredAccessibility))
                continue;

            switch (member)
            {
                case IEventSymbol:
                case IFieldSymbol:
                case IPropertySymbol:
                    result.Add(member);
                    break;

                case IMethodSymbol m when
                    m.MethodKind is MethodKind.Constructor or MethodKind.Ordinary
                    || IsOperator(m):
                    result.Add(member);
                    break;
            }
        }
        return result;
    }

    void AppendMemberDoc(ISymbol member, StringBuilder sb, string prefix, bool isThirdParty)
    {
        if (!isThirdParty) return;
        var doc = xmlDoc.ExtractSymbolDoc(member, prefix);
        if (doc is not null)
            sb.AppendLine(doc);
    }

    static bool IsOperator(IMethodSymbol m) =>
        m.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion;

    // Returns true for all accessibilities visible outside the declaring type (public, internal, protected variants).
    static bool IsVisible(Accessibility a) => a is
        Accessibility.Public or
        Accessibility.Internal or
        Accessibility.Protected or
        Accessibility.ProtectedOrInternal or
        Accessibility.ProtectedAndInternal;

    // Returns the visibility prefix string for all visible members.


    // Returns the full type declaration header: visibility + modifiers + kind keyword + name.
    // Example: "public abstract class AnimalBase", "internal sealed class Utils", "public interface IAnimal"
    static string FormatTypeHeader(INamedTypeSymbol type) => TypeDeclarationFormatter.FormatHeader(type);

    static string FormatTypeLocation(INamedTypeSymbol type)
    {
        var syntaxRef = type.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is not null)
        {

            var line = syntaxRef.SyntaxTree
                .GetLineSpan(syntaxRef.Span)
                .StartLinePosition.Line + 1;

            return $"// source: {syntaxRef.SyntaxTree.FilePath}, line {line}";
        }
        return $"// assembly: {type.ContainingAssembly?.Name}";
    }

    static bool IsWellKnownFrameworkType(INamedTypeSymbol type) =>
        WellKnownFrameworkTypes.IsWellKnown(type);

    void DescribeHierarchy(
        INamedTypeSymbol type, StringBuilder sb, int indent, HashSet<string> visited, bool inheritedCompact, string[]? memberFilters)
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
                Describe(type.BaseType, sb, indent + 2, visited, memberFilters, inheritedCompact);
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
                    Describe(iface, sb, indent + 2, visited, memberFilters, inheritedCompact);
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

    static void DescribeDerivedTypes(INamedTypeSymbol type, ICachedSolution cachedSolution, StringBuilder sb)
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
            sb.AppendLine($"  {d.ToDisplayString()} {FormatTypeLocation(d)}");
    }
}
