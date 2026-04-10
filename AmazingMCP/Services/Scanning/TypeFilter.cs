using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.Scanning;

public class TypeFilter : ITypeFilter
{
    static readonly HashSet<string> ExcludedNames =
    [
        "System.IDisposable",
        "System.IAsyncDisposable",
        "System.ICloneable",
        "System.IComparable",
        "System.IFormattable",
        "System.IConvertible",
        "System.IEquatable",
        "System.IObservable",
        "System.IObserver",
        "System.IServiceProvider",
        "System.Object"
    ];

    static readonly string[] ExcludedPrefixes =
    [
        "System.",
        "Microsoft.Extensions.Options.",
        "Microsoft.Extensions.Logging.",
        "Microsoft.AspNetCore.",
        "Microsoft.EntityFrameworkCore."
    ];

    public bool ShouldExclude(INamedTypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None) return true;
        if (type.TypeKind is TypeKind.Enum or TypeKind.Struct) return true;
        return ShouldExcludeByName(type.ToDisplayString());
    }

    public bool ShouldExcludeByName(string fullName)
    {
        var nameWithoutGenerics = fullName;
        var idx = fullName.IndexOf('<');
        if (idx >= 0) nameWithoutGenerics = fullName[..idx];

        if (ExcludedNames.Contains(nameWithoutGenerics)) return true;

        foreach (var prefix in ExcludedPrefixes)
            if (fullName.StartsWith(prefix, StringComparison.Ordinal)) return true;

        return false;
    }
}
