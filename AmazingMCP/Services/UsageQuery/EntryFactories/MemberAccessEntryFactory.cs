using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery.EntryFactories;

internal static class MemberAccessEntryFactory
{
    internal static QueryEntry? FromMemberAccess(MemberAccessExpressionSyntax node, SemanticModel model)
    {
        // Skip if this is the expression part of an invocation — handled by InvocationEntryFactory
        if (node.Parent is InvocationExpressionSyntax inv && inv.Expression == node)
            return null;

        var symbol = model.GetSymbolInfo(node).Symbol;
        return symbol switch
        {
            IPropertySymbol prop => BuildPropertyEntry(node, prop, model),
            IFieldSymbol field   => BuildFieldEntry(node, field, model),
            _                    => null,
        };
    }

    internal static QueryEntry? FromEventAssignment(AssignmentExpressionSyntax node, SemanticModel model)
    {
        var kind = node.Kind();
        if (kind is not SyntaxKind.AddAssignmentExpression and not SyntaxKind.SubtractAssignmentExpression)
            return null;

        if (model.GetSymbolInfo(node.Left).Symbol is not IEventSymbol eventSymbol)
            return null;

        var receiverType = node.Left is MemberAccessExpressionSyntax ma
            ? model.GetTypeInfo(ma.Expression).Type?.ToDisplayString()
            : null;

        return new QueryEntry
        {
            Kind = kind == SyntaxKind.AddAssignmentExpression ? UsageKind.EventSubscribe : UsageKind.EventUnsubscribe,
            TypeName = receiverType ?? eventSymbol.ContainingType.ToDisplayString(),
            EventName = eventSymbol.Name,
        };
    }

    static QueryEntry BuildPropertyEntry(MemberAccessExpressionSyntax node, IPropertySymbol prop, SemanticModel model)
    {
        var receiverType = model.GetTypeInfo(node.Expression).Type;
        return new QueryEntry
        {
            Kind = QueryEntryFactory.IsWriteTarget(node) ? UsageKind.PropertyWrite : UsageKind.PropertyRead,
            TypeName = receiverType?.ToDisplayString() ?? prop.ContainingType.ToDisplayString(),
            PropertyName = prop.Name,
        };
    }

    static QueryEntry BuildFieldEntry(MemberAccessExpressionSyntax node, IFieldSymbol field, SemanticModel model)
    {
        var receiverType = model.GetTypeInfo(node.Expression).Type;
        return new QueryEntry
        {
            Kind = QueryEntryFactory.IsWriteTarget(node) ? UsageKind.FieldWrite : UsageKind.FieldRead,
            TypeName = receiverType?.ToDisplayString() ?? field.ContainingType.ToDisplayString(),
            FieldName = field.Name,
        };
    }
}
