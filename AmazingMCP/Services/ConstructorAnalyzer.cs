using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public class ConstructorAnalyzer : IConstructorAnalyzer
{
    public List<ConstructorDependency> AnalyzeDependencies(INamedTypeSymbol cls)
    {
        var result = new List<ConstructorDependency>();

        var ctor = cls.Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (ctor is null) return result;

        foreach (var param in ctor.Parameters)
        {
            var dep = ResolveConstructorDependency(param.Type);
            if (dep is not null)
                result.Add(dep);
        }

        return result;
    }

    static ConstructorDependency? ResolveConstructorDependency(ITypeSymbol paramType)
    {
        if (paramType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var genericName = namedType.ConstructedFrom.ToDisplayString();

            if (genericName.StartsWith("Microsoft.Extensions.Options.IOptions<", StringComparison.Ordinal) ||
                genericName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot<", StringComparison.Ordinal) ||
                genericName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor<", StringComparison.Ordinal))
            {
                return new ConstructorDependency(
                    namedType.TypeArguments[0].ToDisplayString(), IsOptions: true, IsEnumerable: false);
            }

            if (IsEnumerableInterface(namedType))
            {
                return new ConstructorDependency(
                    namedType.TypeArguments[0].ToDisplayString(), IsOptions: false, IsEnumerable: true);
            }
        }

        if (paramType.SpecialType != SpecialType.None) return null;
        if (paramType.TypeKind is TypeKind.Enum or TypeKind.Struct) return null;

        return new ConstructorDependency(paramType.ToDisplayString(), IsOptions: false, IsEnumerable: false);
    }

    static bool IsEnumerableInterface(INamedTypeSymbol type) =>
        type.IsGenericType &&
        type.ConstructedFrom.ToDisplayString()
            .StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal);
}
