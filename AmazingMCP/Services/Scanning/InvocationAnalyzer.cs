using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.Scanning;

public class InvocationAnalyzer : IInvocationAnalyzer
{
    public (INamedTypeSymbol ContainingType, string MemberName, bool IsStatic)?
        Analyze(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method) return null;

        // Skip constructors, operators, property accessors, etc.
        if (method.MethodKind is MethodKind.Constructor
            or MethodKind.StaticConstructor
            or MethodKind.PropertyGet
            or MethodKind.PropertySet
            or MethodKind.EventAdd
            or MethodKind.EventRemove
            or MethodKind.UserDefinedOperator
            or MethodKind.Conversion) return null;

        // Extension method: use receiver type, not the static declaring class
        var isExtension = method.MethodKind == MethodKind.ReducedExtension
            || method.ReducedFrom is not null;

        if (isExtension)
        {
            var receiverType = ResolveReceiverType(invocation, model);
            if (receiverType is null) return null;
            return (receiverType, method.Name, false);
        }

        // Static call: ContainingType is the static class
        if (method.IsStatic)
        {
            if (method.ContainingType is not { } containingType) return null;
            return (containingType, method.Name, true);
        }

        // Regular instance call
        if (method.ContainingType is not { } ct) return null;
        return (ct, method.Name, false);
    }

    static INamedTypeSymbol? ResolveReceiverType(
        InvocationExpressionSyntax invocation, SemanticModel model)
    {
        // For extension method calls like _logger.LogInformation(...)
        // the receiver is the first argument in the original definition,
        // but in reduced form it's the expression before the dot.
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var typeInfo = model.GetTypeInfo(memberAccess.Expression);
            return typeInfo.Type as INamedTypeSymbol;
        }
        return null;
    }
}
