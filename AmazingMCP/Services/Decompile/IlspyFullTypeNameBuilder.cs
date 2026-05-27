using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.Decompile;

/// <summary>
/// Builds an ILSpy <see cref="FullTypeName"/> from a Roslyn <see cref="INamedTypeSymbol"/>.
/// Uses <see cref="TopLevelTypeName"/> directly so that generic arity is expressed correctly
/// (e.g. <c>OptionsManager`1</c>) rather than as C# angle-bracket syntax.
/// </summary>
public static class IlspyFullTypeNameBuilder
{
    public static FullTypeName Build(INamedTypeSymbol symbol)
    {
        var top = GetTopLevel(symbol);

        var ns = top.ContainingNamespace is { IsGlobalNamespace: false } n
            ? n.ToDisplayString()
            : "";

        var topLevelName = new TopLevelTypeName(ns, top.Name, top.Arity);
        var fullName = new FullTypeName(topLevelName);

        // Walk nested types from the level below top down to symbol
        foreach (var nested in GetNestingChain(symbol))
            fullName = fullName.NestedType(nested.Name, nested.Arity);

        return fullName;
    }

    static INamedTypeSymbol GetTopLevel(INamedTypeSymbol symbol)
    {
        var current = symbol;
        while (current.ContainingType is not null)
            current = current.ContainingType;
        return current;
    }

    // Returns the chain of nested types from just below top-level down to symbol (inclusive).
    static IEnumerable<INamedTypeSymbol> GetNestingChain(INamedTypeSymbol symbol)
    {
        var stack = new Stack<INamedTypeSymbol>();
        for (var current = symbol; current.ContainingType is not null; current = current.ContainingType)
            stack.Push(current);
        return stack;
    }
}
