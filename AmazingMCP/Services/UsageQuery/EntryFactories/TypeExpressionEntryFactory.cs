using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery.EntryFactories;

internal static class TypeExpressionEntryFactory
{
    internal static QueryEntry? FromObjectCreation(ObjectCreationExpressionSyntax node, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
        if (symbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.ConstructorCall,
            TypeName = symbol.ContainingType.ToDisplayString(),
            MethodName = symbol.ContainingType.Name,
            ArgumentTypes = QueryEntryFactory.GetArgumentTypes(node.ArgumentList, model),
        };
    }

    internal static QueryEntry? FromImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax node, SemanticModel model)
    {
        if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol) return null;

        return new QueryEntry
        {
            Kind = UsageKind.ConstructorCall,
            TypeName = symbol.ContainingType.ToDisplayString(),
            MethodName = symbol.ContainingType.Name,
            ArgumentTypes = QueryEntryFactory.GetArgumentTypes(node.ArgumentList, model),
        };
    }

    internal static QueryEntry? FromGenericName(GenericNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not TypeArgumentListSyntax) return null;
        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.GenericArgument, TypeName = typeSymbol.ToDisplayString() };
    }

    internal static QueryEntry? FromTypeConstraint(TypeConstraintSyntax node, SemanticModel model)
    {
        var typeSymbol = model.GetTypeInfo(node.Type).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.GenericConstraint, TypeName = typeSymbol.ToDisplayString() };
    }

    internal static QueryEntry? FromParameter(ParameterSyntax node, SemanticModel model)
    {
        if (node.Type is null) return null;
        var typeSymbol = model.GetTypeInfo(node.Type).Type;
        if (typeSymbol is null) return null;

        // Unwrap nullable: IRequest? → IRequest
        var namedType = typeSymbol is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } nullable
            ? nullable.TypeArguments.FirstOrDefault() ?? typeSymbol
            : typeSymbol;

        return new QueryEntry { Kind = UsageKind.Parameter, TypeName = namedType.ToDisplayString() };
    }

    internal static QueryEntry? FromTypeOf(TypeOfExpressionSyntax node, SemanticModel model)
    {
        var typeSymbol = model.GetTypeInfo(node.Type).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.TypeOf, TypeName = typeSymbol.ToDisplayString() };
    }

    internal static QueryEntry? FromBinaryExpression(BinaryExpressionSyntax node, SemanticModel model)
    {
        if (!node.IsKind(SyntaxKind.IsExpression) && !node.IsKind(SyntaxKind.AsExpression))
            return null;

        var typeSymbol = model.GetTypeInfo(node.Right).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.IsOrAs, TypeName = typeSymbol.ToDisplayString() };
    }
}
