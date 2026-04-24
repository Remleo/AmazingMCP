using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>Walks Roslyn namespace trees and collects matching types and members.</summary>
static class SymbolQueryCollector
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
            if (!IsMemberSearchCandidate(typeSymbol))
                continue;

            var declaringType = SymbolResultFactory.ForType(typeSymbol);
            var assembly = typeSymbol.ContainingAssembly?.Name ?? "unknown";

            foreach (var member in typeSymbol.GetMembers())
            {
                if (!IsVisibleMember(member))
                    continue;

                if (member is IMethodSymbol method)
                    TryAddMethod(method, declaringType, assembly, pattern, seen, results);
                else if (member is IPropertySymbol property)
                    TryAddProperty(property, declaringType, assembly, pattern, seen, results);
            }
        }
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

    // Only search members on classes and interfaces; skip well-known framework types.
    static bool IsMemberSearchCandidate(INamedTypeSymbol type) =>
        (type.TypeKind is TypeKind.Class or TypeKind.Interface) &&
        !WellKnownFrameworkTypes.IsWellKnown(type);

    static bool IsVisibleMember(ISymbol member) =>
        member.DeclaredAccessibility is
            Accessibility.Public or
            Accessibility.Internal or
            Accessibility.Protected or
            Accessibility.ProtectedOrInternal;
}
