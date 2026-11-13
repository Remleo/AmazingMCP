using AmazingMCP.Models;
using AmazingMCP.Services.Wildcard;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>Walks Roslyn namespace trees and collects matching types and members.</summary>
internal static class SymbolWalker
{
    public static void CollectType(
        INamedTypeSymbol typeSymbol,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results,
        IReadOnlyList<Version?> nugetVersions)
    {
        var key = SeenSymbolKey.ForType(
            typeDisplayName: typeSymbol.ToDisplayString(),
            assembly: typeSymbol.ContainingAssembly?.Name ?? "unknown");
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForType(typeSymbol, nugetVersions));
    }

    public static void CollectMembers(
        INamedTypeSymbol typeSymbol,
        IReadOnlyList<IWildcardPattern> memberPatterns,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results,
        IReadOnlyList<Version?> nugetVersions)
    {
        if (WellKnownFrameworkTypes.IsWellKnown(typeSymbol))
            return;

        var declaringType = SymbolResultFactory.ForType(typeSymbol, nugetVersions);
        var assembly = typeSymbol.ContainingAssembly?.Name ?? "unknown";

        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
                TryAddEnumValue(member, declaringType, assembly, memberPatterns, seen, results);
            return;
        }

        if (!IsClassOrInterface(typeSymbol))
            return;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (!IsVisibleMember(member))
                continue;

            if (member is IMethodSymbol method)
                TryAddMethod(method, declaringType, assembly, memberPatterns, seen, results);
            else if (member is IPropertySymbol property)
                TryAddProperty(property, declaringType, assembly, memberPatterns, seen, results);
            else if (member is IFieldSymbol field)
                TryAddField(field, declaringType, assembly, memberPatterns, seen, results);
            else if (member is IEventSymbol evt)
                TryAddEvent(evt, declaringType, assembly, memberPatterns, seen, results);
        }
    }

    static void TryAddEnumValue(
        IFieldSymbol field,
        SymbolResult declaringType,
        string assembly,
        IReadOnlyList<IWildcardPattern> memberPatterns,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!IsNameMatch(field.Name, memberPatterns)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: field.ContainingType.ToDisplayString(),
            memberSignature: field.Name,
            assembly: assembly);
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForEnumValue(field, declaringType));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        SymbolResult declaringType,
        string assembly,
        IReadOnlyList<IWildcardPattern> memberPatterns,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (method.MethodKind != MethodKind.Ordinary) return;
        if (!IsNameMatch(method.Name, memberPatterns)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: method.ContainingType.ToDisplayString(),
            memberSignature: SymbolResultFactory.MethodSignature(method),
            assembly: assembly);
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForMethod(method, declaringType));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        SymbolResult declaringType,
        string assembly,
        IReadOnlyList<IWildcardPattern> memberPatterns,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!IsNameMatch(property.Name, memberPatterns)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: property.ContainingType.ToDisplayString(),
            memberSignature: SymbolResultFactory.PropertySignature(property),
            assembly: assembly);
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForProperty(property, declaringType));
    }

    static void TryAddField(
        IFieldSymbol field,
        SymbolResult declaringType,
        string assembly,
        IReadOnlyList<IWildcardPattern> memberPatterns,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!IsNameMatch(field.Name, memberPatterns)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: field.ContainingType.ToDisplayString(),
            memberSignature: field.Name,
            assembly: assembly);
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForField(field, declaringType));
    }

    static void TryAddEvent(
        IEventSymbol evt,
        SymbolResult declaringType,
        string assembly,
        IReadOnlyList<IWildcardPattern> memberPatterns,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!IsNameMatch(evt.Name, memberPatterns)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: evt.ContainingType.ToDisplayString(),
            memberSignature: evt.Name,
            assembly: assembly);
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForEvent(evt, declaringType));
    }

    static bool IsNameMatch(string name, IReadOnlyList<IWildcardPattern> patterns) =>
        patterns.Any(p => p.IsMatch(name));

    static bool IsClassOrInterface(INamedTypeSymbol type) =>
        type.TypeKind is TypeKind.Class or TypeKind.Interface;

    static bool IsVisibleMember(ISymbol member)
    {
        if (member.Locations.Any(l => l.IsInSource))
        {
            if (member is IFieldSymbol or IPropertySymbol or IEventSymbol)
                return member.DeclaredAccessibility is not Accessibility.Private;

            return true;
        }

        return member.DeclaredAccessibility is not (Accessibility.Private or Accessibility.ProtectedAndInternal);
    }
}
