using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

/// <summary>Converts Roslyn symbols into <see cref="SymbolResult"/> records.</summary>
static class SymbolResultFactory
{
    public static SymbolResult ForType(INamedTypeSymbol symbol)
    {
        var (path, line) = SourceLocation(symbol.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = symbol.Name,
            FullName = symbol.ToDisplayString(),
            Kind = symbol.TypeKind.ToString(),
            ContainingAssembly = symbol.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
        };
    }

    public static SymbolResult ForMethod(IMethodSymbol method, SymbolResult declaringType)
    {
        var (path, line) = SourceLocation(method.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = method.Name,
            FullName = MethodSignature(method),
            Kind = "Method",
            ContainingAssembly = method.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
            DeclaringType = declaringType,
        };
    }

    public static SymbolResult ForProperty(IPropertySymbol property, SymbolResult declaringType)
    {
        var (path, line) = SourceLocation(property.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = property.Name,
            FullName = PropertySignature(property),
            Kind = "Property",
            ContainingAssembly = property.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
            DeclaringType = declaringType,
        };
    }

    public static SymbolResult ForEnumValue(IFieldSymbol field, SymbolResult declaringType)
    {
        var (path, line) = SourceLocation(field.DeclaringSyntaxReferences.FirstOrDefault());
        return new()
        {
            Name = field.Name,
            FullName = field.Name,
            Kind = "EnumValue",
            ContainingAssembly = field.ContainingAssembly?.Name,
            SourceFilePath = path,
            DefinitionLine = line,
            DeclaringType = declaringType,
        };
    }

    public static string MethodSignature(IMethodSymbol method) =>
        method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithMemberOptions(
                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeType)
            .WithParameterOptions(
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeName));

    public static string PropertySignature(IPropertySymbol property) =>
        property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithMemberOptions(SymbolDisplayMemberOptions.IncludeType));

    static (string? Path, int? Line) SourceLocation(SyntaxReference? syntaxRef)
    {
        if (syntaxRef is null) return (null, null);
        return (
            syntaxRef.SyntaxTree.FilePath,
            syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span).StartLinePosition.Line + 1);
    }
}
