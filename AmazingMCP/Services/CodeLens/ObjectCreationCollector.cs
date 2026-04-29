using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.CodeLens;

internal static class ObjectCreationCollector
{
    internal static void Collect(
        ObjectCreationExpressionSyntax node,
        SemanticModel model,
        HashSet<ConstructorKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetTypeInfo(node).Type is not INamedTypeSymbol namedType) return;
        AddEntry(namedType, model.GetSymbolInfo(node).Symbol as IMethodSymbol, node.SpanStart, model, seen, output);
    }

    internal static void CollectImplicit(
        ImplicitObjectCreationExpressionSyntax node,
        SemanticModel model,
        HashSet<ConstructorKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetTypeInfo(node).Type is not INamedTypeSymbol namedType) return;
        AddEntry(namedType, model.GetSymbolInfo(node).Symbol as IMethodSymbol, node.SpanStart, model, seen, output);
    }

    static void AddEntry(
        INamedTypeSymbol namedType,
        IMethodSymbol? ctor,
        int spanStart,
        SemanticModel model,
        HashSet<ConstructorKey> seen,
        List<CodeLensEntry> output)
    {
        var typeName = CodeLensTypeFormatter.GetDisplayName(namedType);
        if (CodeLensTypeChecker.IsTrivialDisplayName(typeName)) return;

        var argCount = ctor?.Parameters.Length ?? 0;
        if (argCount == 0) return;

        var paramTypes = ctor != null ? BuildParamTypes(ctor.Parameters) : [];
        var paramTypesKey = string.Join("|", paramTypes.Select(p => p.TypeName));

        var key = new ConstructorKey(typeName, paramTypesKey);
        if (!seen.Add(key)) return;

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Constructor,
            TypeFullName = typeName,
            TypeShortName = namedType.Name,
            ArgTypes = paramTypes.Count > 0 ? paramTypes.Select(p => p.TypeName).ToList() : null,
            ArgNames = paramTypes.Count > 0 ? paramTypes.Select(p => p.ParamName).ToList() : null,
            ArgCount = argCount,
            SourceLine = CollectorHelpers.GetSourceLine(model, spanStart),
        });
    }

    /// <summary>
    /// Builds a list of (TypeName, ParamName) for all parameters.
    /// </summary>
    static List<(string TypeName, string ParamName)> BuildParamTypes(
        IEnumerable<IParameterSymbol> parameters)
        => parameters
            .Select(p => (TypeName: CodeLensTypeFormatter.GetDisplayName(p.Type), ParamName: p.Name))
            .ToList();
}
