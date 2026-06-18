using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace AmazingMCP.Services.UsageQuery.EntryFactories;

internal static class InvocationEntryFactory
{
    internal static QueryEntry? FromInvocation(InvocationExpressionSyntax node, SemanticModel model)
    {
        if (model.GetOperation(node) is INameOfOperation)
            return NameOfEntryFactory.FromNameOf(node, model);

        var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
        if (symbol is null) return null;

        return TryEventCallFromConditionalInvoke(node, model)
               ?? TryEventCallFromDirectInvocation(node, model)
               ?? MethodCallEntry(node, symbol, model);
    }

    internal static QueryEntry? TryCreateExtensionMethodEntry(SyntaxNode node, QueryEntry receiverEntry, SemanticModel model)
    {
        if (node is not InvocationExpressionSyntax inv) return null;
        if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol { IsExtensionMethod: true } sym)
            return null;

        // For null-conditional calls (obj?.ExtMethod()), report the call on the receiver type, not the extension class
        if (inv.Expression is MemberBindingExpressionSyntax)
        {
            var conditionalAccess = inv.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>();
            if (conditionalAccess is not null)
            {
                var t = model.GetTypeInfo(conditionalAccess.Expression).Type;
                // Strip nullable annotation: IOptionalTracer? → IOptionalTracer
                var receiverType = t?.WithNullableAnnotation(NullableAnnotation.None);
                return new QueryEntry
                {
                    Kind = UsageKind.MethodCall,
                    TypeName = receiverType?.ToDisplayString() ?? sym.ContainingType.ToDisplayString(),
                    MethodName = sym.Name,
                    ArgumentTypes = receiverEntry.ArgumentTypes,
                };
            }
        }

        return new QueryEntry
        {
            Kind = UsageKind.MethodCall,
            TypeName = sym.ContainingType.ToDisplayString(),
            MethodName = sym.Name,
            ArgumentTypes = receiverEntry.ArgumentTypes,
        };
    }

    static QueryEntry? TryEventCallFromConditionalInvoke(InvocationExpressionSyntax node, SemanticModel model)
    {
        if (node.Expression is not MemberBindingExpressionSyntax { Name.Identifier.Text: "Invoke" })
            return null;

        var conditionalAccess = node.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>();
        if (conditionalAccess is null) return null;

        var eventSymbol = model.GetSymbolInfo(conditionalAccess.Expression).Symbol as IEventSymbol;
        if (eventSymbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.EventCall,
            TypeName = eventSymbol.ContainingType.ToDisplayString(),
            EventName = eventSymbol.Name,
        };
    }

    static QueryEntry? TryEventCallFromDirectInvocation(InvocationExpressionSyntax node, SemanticModel model)
    {
        if (node.Expression is not IdentifierNameSyntax id) return null;
        if (model.GetSymbolInfo(id).Symbol is not IEventSymbol eventSymbol) return null;

        return new QueryEntry
        {
            Kind = UsageKind.EventCall,
            TypeName = eventSymbol.ContainingType.ToDisplayString(),
            EventName = eventSymbol.Name,
        };
    }

    static QueryEntry MethodCallEntry(InvocationExpressionSyntax node, IMethodSymbol symbol, SemanticModel model)
    {
        var typeName = node.Expression is MemberAccessExpressionSyntax memberAccess
            ? model.GetTypeInfo(memberAccess.Expression).Type?.ToDisplayString() ?? symbol.ContainingType.ToDisplayString()
            : symbol.ContainingType.ToDisplayString();

        return new QueryEntry
        {
            Kind = UsageKind.MethodCall,
            TypeName = typeName,
            MethodName = symbol.Name,
            ArgumentTypes = QueryEntryFactory.GetArgumentTypes(node.ArgumentList, model),
        };
    }
}