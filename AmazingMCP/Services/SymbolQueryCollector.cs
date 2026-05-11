using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>Walks Roslyn namespace trees and collects matching types and members.</summary>
internal static class SymbolQueryCollector
{
    public static void CollectTypes(
        INamespaceSymbol ns,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        foreach (var typeSymbol in RoslynTypeEnumerator.FindNamedTypes(ns, pattern))
        {
            var key = SeenSymbolKey.ForType(
                typeDisplayName: typeSymbol.ToDisplayString(),
                assembly: typeSymbol.ContainingAssembly?.Name ?? "unknown");
            if (seen.Add(key))
                results.Add(SymbolResultFactory.ForType(typeSymbol));
        }
    }

    public static void CollectMembers(
        INamespaceSymbol ns,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        foreach (var typeSymbol in RoslynTypeEnumerator.EnumerateAll(ns))
        {
            if (WellKnownFrameworkTypes.IsWellKnown(typeSymbol))
                continue;

            var declaringType = SymbolResultFactory.ForType(typeSymbol);
            var assembly = typeSymbol.ContainingAssembly?.Name ?? "unknown";

            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
                    TryAddEnumValue(member, declaringType, assembly, pattern, seen, results);
                continue;
            }

            if (!IsClassOrInterface(typeSymbol))
                continue;

            foreach (var member in typeSymbol.GetMembers())
            {
                if (!IsVisibleMember(member))
                    continue;

                if (member is IMethodSymbol method)
                    TryAddMethod(method, declaringType, assembly, pattern, seen, results);
                else if (member is IPropertySymbol property)
                    TryAddProperty(property, declaringType, assembly, pattern, seen, results);
                else if (member is IFieldSymbol field)
                    TryAddField(field, declaringType, assembly, pattern, seen, results);
                else if (member is IEventSymbol evt)
                    TryAddEvent(evt, declaringType, assembly, pattern, seen, results);
            }
        }
    }

    static void TryAddEnumValue(
        IFieldSymbol field,
        SymbolResult declaringType,
        string assembly,
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!pattern.IsMatch(field.Name)) return;
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
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (method.MethodKind != MethodKind.Ordinary) return;
        if (!pattern.IsMatch(method.Name)) return;
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
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!pattern.IsMatch(property.Name)) return;
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
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!pattern.IsMatch(field.Name)) return;
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
        IWildcardPattern pattern,
        HashSet<SeenSymbolKey> seen,
        List<SymbolResult> results)
    {
        if (!pattern.IsMatch(evt.Name)) return;
        var key = SeenSymbolKey.ForMember(
            containingTypeDisplayName: evt.ContainingType.ToDisplayString(),
            memberSignature: evt.Name,
            assembly: assembly);
        if (seen.Add(key))
            results.Add(SymbolResultFactory.ForEvent(evt, declaringType));
    }

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
