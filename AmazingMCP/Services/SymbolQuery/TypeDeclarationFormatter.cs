using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Formats <see cref="INamedTypeSymbol"/> declarations as C# header strings.
/// </summary>
public static class TypeDeclarationFormatter
{
    /// <summary>
    /// Returns a C# declaration header for the type, e.g. "public abstract class MyApp.Core.Animal".
    /// </summary>
    public static string FormatHeader(INamedTypeSymbol type)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(FormatVisibility(type.DeclaredAccessibility));

        if (type.IsStatic) sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class) sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class) sb.Append("sealed ");

        sb.Append(type.TypeKind switch
        {
            TypeKind.Interface => "interface ",
            TypeKind.Enum      => "enum ",
            TypeKind.Struct    => "struct ",
            TypeKind.Delegate  => "delegate ",
            _                  => "class ",
        });

        sb.Append(type.ToDisplayString());
        return sb.ToString();
    }

    public static string FormatVisibility(Accessibility a) => a switch
    {
        Accessibility.Public              => "public ",
        Accessibility.Internal            => "internal ",
        Accessibility.Protected           => "protected ",
        Accessibility.ProtectedOrInternal => "protected internal ",
        Accessibility.ProtectedAndInternal => "private protected ",
        Accessibility.Private             => "private ",
        _                                 => "",
    };
}
