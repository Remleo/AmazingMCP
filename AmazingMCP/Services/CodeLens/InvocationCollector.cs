using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.CodeLens;

internal static class InvocationCollector
{
    internal static void Collect(
        InvocationExpressionSyntax node,
        SemanticModel model,
        INamedTypeSymbol? containingType,
        HashSet<CallKey> seenCalls,
        HashSet<ExtensionKey> seenExtensions,
        List<CodeLensEntry> calls,
        List<CodeLensEntry> extensions)
    {
        if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol method) return;

        var isExtension = method.IsExtensionMethod || method.ReducedFrom != null;

        if (isExtension)
            CollectExtension(method, node.SpanStart, model, containingType, seenExtensions, extensions);
        else
            CollectCall(method, node.SpanStart, model, containingType, seenCalls, calls);
    }

    static void CollectCall(
        IMethodSymbol method,
        int spanStart,
        SemanticModel model,
        INamedTypeSymbol? containingType,
        HashSet<CallKey> seen,
        List<CodeLensEntry> output)
    {
        var declaringType = method.ContainingType;

        // Skip methods declared in System.* types
        if (IsSystemType(declaringType)) return;

        var paramTypes = BuildParamTypes(method.Parameters);
        var paramTypesKey = string.Join("|", paramTypes.Select(p => p.TypeName));
        var declaringTypeName = CodeLensTypeFormatter.GetDisplayName(declaringType);

        var key = new CallKey(method.Name, paramTypesKey, declaringTypeName);
        if (!seen.Add(key)) return;

        var returnType = CodeLensTypeFormatter.GetDisplayName(method.ReturnType);
        var isSameClass = containingType != null &&
                          SymbolEqualityComparer.Default.Equals(declaringType, containingType);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Call,
            MethodName = method.Name,
            ReturnType = returnType,
            ArgTypes = paramTypes.Count > 0 ? paramTypes.Select(p => p.TypeName).ToList() : null,
            ArgNames = paramTypes.Count > 0 ? paramTypes.Select(p => p.ParamName).ToList() : null,
            ArgCount = method.Parameters.Length,
            DeclaringType = isSameClass ? null : declaringTypeName,
            SourceLine = CollectorHelpers.GetSourceLine(model, spanStart),
        });
    }

    static void CollectExtension(
        IMethodSymbol method,
        int spanStart,
        SemanticModel model,
        INamedTypeSymbol? containingType,
        HashSet<ExtensionKey> seen,
        List<CodeLensEntry> output)
    {
        var original = method.ReducedFrom ?? method;
        var declaringType = original.ContainingType;

        // Skip extension methods declared in System.* types
        if (IsSystemType(declaringType)) return;

        var receiverType = CodeLensTypeFormatter.GetDisplayName(original.Parameters[0].Type);
        var receiverParamName = original.Parameters[0].Name;
        var extParams = original.Parameters.Skip(1).ToArray();
        var paramTypes = BuildParamTypes(extParams);
        var paramTypesKey = string.Join("|", paramTypes.Select(p => p.TypeName));
        var declaringTypeName = CodeLensTypeFormatter.GetDisplayName(declaringType);

        var key = new ExtensionKey(method.Name, paramTypesKey, declaringTypeName);
        if (!seen.Add(key)) return;

        var returnType = CodeLensTypeFormatter.GetDisplayName(method.ReturnType);
        var isSameClass = containingType != null &&
                          SymbolEqualityComparer.Default.Equals(declaringType, containingType);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Extension,
            MethodName = method.Name,
            ReturnType = returnType,
            ArgTypes = paramTypes.Count > 0 ? paramTypes.Select(p => p.TypeName).ToList() : null,
            ArgNames = paramTypes.Count > 0 ? paramTypes.Select(p => p.ParamName).ToList() : null,
            ArgCount = extParams.Length,
            ReceiverType = CodeLensTypeChecker.IsTrivialDisplayName(receiverType) ? null : receiverType,
            ReceiverParamName = receiverParamName,
            DeclaringType = isSameClass ? null : declaringTypeName,
            SourceLine = CollectorHelpers.GetSourceLine(model, spanStart),
        });
    }

    static List<(string TypeName, string ParamName)> BuildParamTypes(
        IEnumerable<IParameterSymbol> parameters)
        => parameters
            .Select(p => (TypeName: CodeLensTypeFormatter.GetDisplayName(p.Type), ParamName: p.Name))
            .ToList();

    static bool IsSystemType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.StartsWith("System", StringComparison.Ordinal);
    }
}
