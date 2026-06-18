using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery.EntryFactories;

internal static class NameOfEntryFactory
{
    internal static QueryEntry? FromNameOf(InvocationExpressionSyntax node, SemanticModel model)
    {
        var arg = node.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (arg is null) return null;

        // nameof(Animal.Name) or nameof(Animal)
        if (arg is MemberAccessExpressionSyntax memberAccess)
        {
            var containingType = model.GetTypeInfo(memberAccess.Expression).Type;
            if (containingType is null) return null;

            var memberSymbol = model.GetSymbolInfo(memberAccess).Symbol;
            return new QueryEntry
            {
                Kind = UsageKind.NameOf,
                TypeName = containingType.ToDisplayString(),
                MethodName   = memberSymbol is IMethodSymbol   m ? m.Name : null,
                PropertyName = memberSymbol is IPropertySymbol p ? p.Name : null,
                FieldName    = memberSymbol is IFieldSymbol    f ? f.Name : null,
            };
        }

        // nameof(Animal) — type only, or nameof(GetThisMethodName) — member of current class
        // Note: for method groups, GetSymbolInfo returns no Symbol but CandidateSymbols has the method
        var symbolInfo = model.GetSymbolInfo(arg);
        var symbol = symbolInfo.Symbol
                  ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (symbol is ITypeSymbol typeSymbol)
            return new QueryEntry { Kind = UsageKind.NameOf, TypeName = typeSymbol.ToDisplayString() };

        if (symbol is not null && symbol.ContainingType is not null)
            return new QueryEntry
            {
                Kind = UsageKind.NameOf,
                TypeName = symbol.ContainingType.ToDisplayString(),
                MethodName   = symbol is IMethodSymbol   m ? m.Name : null,
                PropertyName = symbol is IPropertySymbol p ? p.Name : null,
                FieldName    = symbol is IFieldSymbol    f ? f.Name : null,
            };

        return null;
    }
}
