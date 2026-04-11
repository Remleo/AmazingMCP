using Microsoft.CodeAnalysis;

namespace AmazingMCP.Models;

/// <summary>
/// All data extracted from an INamedTypeSymbol needed for building the dependency map.
/// Created once from a Roslyn symbol — no Roslyn references escape this type.
/// Used during map construction; not stored in the final DependencyMapResult.
/// </summary>
public record RawTypeInfo(
    string FullName,
    string Namespace,
    string AssemblyName,
    string? SourceFilePath,
    bool IsInterface,
    bool IsAbstractClass,
    bool IsStaticClass,
    bool IsGeneric,
    /// <summary>
    /// For closed generic types (e.g. ITracer&lt;FooService&gt;): the open generic display name
    /// (e.g. "Bwin...ITracer&lt;TService&gt;"). Null for non-generic or open generic types.
    /// </summary>
    string? OpenGenericFullName,
    /// <summary>
    /// For closed generic types: the open generic metadata name (e.g. "ITracer`1").
    /// Used as fallback key for GetTypeByMetadataName. Null for non-generic or open generic types.
    /// </summary>
    string? OpenGenericMetadataName,
    /// <summary>
    /// True for primitives (string, int, bool, etc.), enums, and structs — must be excluded from the map.
    /// Mirrors ITypeFilter.ShouldExclude(INamedTypeSymbol) without requiring the symbol at call site.
    /// </summary>
    bool IsSpecialType = false)
{
    public static RawTypeInfo From(INamedTypeSymbol symbol, string? sourcePath = null)
    {
        var isClosedGeneric = symbol.IsGenericType
            && symbol.TypeArguments.Length > 0
            && symbol.TypeArguments.All(a => a.Kind != SymbolKind.TypeParameter);

        string? openGenericFullName = null;
        string? openGenericMetadataName = null;

        if (isClosedGeneric)
        {
            var orig = symbol.OriginalDefinition;
            openGenericFullName = orig.ToDisplayString();
            openGenericMetadataName = orig.MetadataName; // e.g. "ITracer`1"
        }

        return new RawTypeInfo(
            FullName: symbol.ToDisplayString(),
            Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
            AssemblyName: symbol.ContainingAssembly?.Name ?? "",
            SourceFilePath: sourcePath ?? GetSourcePath(symbol),
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            IsAbstractClass: symbol.TypeKind == TypeKind.Class && symbol.IsAbstract,
            IsStaticClass: symbol.TypeKind == TypeKind.Class && symbol.IsStatic,
            IsGeneric: symbol.IsGenericType,
            OpenGenericFullName: openGenericFullName,
            OpenGenericMetadataName: openGenericMetadataName,
            // Primitives (string, int, bool, etc.) have SpecialType != None — must be excluded
            IsSpecialType: symbol.SpecialType != SpecialType.None
                || symbol.TypeKind is TypeKind.Enum or TypeKind.Struct);
    }

    static string? GetSourcePath(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
}
