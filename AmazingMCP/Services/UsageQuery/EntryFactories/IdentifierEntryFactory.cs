using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery.EntryFactories;

internal static class IdentifierEntryFactory
{
    internal static QueryEntry? FromIdentifier(IdentifierNameSyntax node, SemanticModel model)
    {
        return TryFromTypeArgument(node, model)
               ?? TryFromObjectInitializerLeft(node, model)
               ?? TryFromObjectInitializerRight(node, model)
               ?? TryFromSymbol(node, model);
    }

    static QueryEntry? TryFromTypeArgument(IdentifierNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not TypeArgumentListSyntax) return null;
        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.GenericArgument, TypeName = typeSymbol.ToDisplayString() };
    }

    static QueryEntry? TryFromObjectInitializerLeft(IdentifierNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not AssignmentExpressionSyntax assign
            || assign.Left != node
            || assign.Parent is not InitializerExpressionSyntax) return null;

        return model.GetSymbolInfo(node).Symbol switch
        {
            IPropertySymbol p => new QueryEntry { Kind = UsageKind.PropertyWrite, TypeName = p.ContainingType.ToDisplayString(), PropertyName = p.Name },
            IFieldSymbol f => new QueryEntry { Kind = UsageKind.FieldWrite, TypeName = f.ContainingType.ToDisplayString(), FieldName = f.Name },
            _ => null,
        };
    }

    static QueryEntry? TryFromObjectInitializerRight(IdentifierNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not AssignmentExpressionSyntax assign
            || assign.Right != node
            || assign.Parent is not InitializerExpressionSyntax) return null;

        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry
        {
            Kind = UsageKind.PropertyWrite,
            TypeName = typeSymbol.ToDisplayString(),
            PropertyName = assign.Left is IdentifierNameSyntax lhs ? lhs.Identifier.Text : null,
        };
    }

    static QueryEntry? TryFromSymbol(IdentifierNameSyntax node, SemanticModel model)
    {
        if (QueryEntryFactory.IsDeclarationContext(node)) return null;

        if (node.Parent is MemberAccessExpressionSyntax ma && ma.Name == node) return null;
        if (node.Parent is InvocationExpressionSyntax or QualifiedNameSyntax) return null;

        return model.GetSymbolInfo(node).Symbol switch
        {
            IPropertySymbol prop => new QueryEntry
            {
                Kind = QueryEntryFactory.IsWriteTarget(node) ? UsageKind.PropertyWrite : UsageKind.PropertyRead,
                TypeName = prop.Type.ToDisplayString(),
                PropertyName = prop.Name,
            },
            IFieldSymbol field => new QueryEntry
            {
                Kind = QueryEntryFactory.IsWriteTarget(node) ? UsageKind.FieldWrite : UsageKind.FieldRead,
                TypeName = field.Type.ToDisplayString(),
                FieldName = field.Name,
            },
            _ => TryBuildReturnTypeEntry(node, model),
        };
    }

    internal static QueryEntry? TryBuildReturnTypeEntry(IdentifierNameSyntax node, SemanticModel model)
    {
        var isReturnTypeContext = node.Parent switch
        {
            MethodDeclarationSyntax method => method.ReturnType == node,
            PropertyDeclarationSyntax prop => prop.Type == node,
            VariableDeclarationSyntax varDecl when varDecl.Parent is FieldDeclarationSyntax => varDecl.Type == node,
            _ => false,
        };

        return isReturnTypeContext ? BuildReturnTypeEntry(node, model) : null;
    }

    static QueryEntry? BuildReturnTypeEntry(IdentifierNameSyntax node, SemanticModel model)
    {
        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.ReturnType, TypeName = typeSymbol.ToDisplayString() };
    }
}