using AmazingMCP.Models;
using AmazingMCP.Models.CodeLens;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.CodeLens;

internal static class DefinitionCollector
{
    internal static void CollectMethod(
        MethodDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not IMethodSymbol symbol) return;

        var key = new DefinitionKey(symbol.Name, CodeLensEntryKind.DefinitionMethod);
        if (!seen.Add(key)) return;

        var returnType = CodeLensTypeFormatter.GetDisplayName(symbol.ReturnType);
        var paramTypes = BuildParamTypes(symbol.Parameters);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionMethod,
            MethodName = symbol.Name,
            ReturnType = returnType,
            ArgTypes = paramTypes.Count > 0 ? paramTypes.Select(p => p.TypeName).ToList() : null,
            ArgNames = paramTypes.Count > 0 ? paramTypes.Select(p => p.ParamName).ToList() : null,
            ArgCount = symbol.Parameters.Length,
            SourceLine = CollectorHelpers.GetSourceLine(model, node.Identifier.SpanStart),
        });
    }

    internal static void CollectConstructor(
        ConstructorDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not IMethodSymbol symbol) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(symbol.ContainingType);
        var key = new DefinitionKey(typeName, CodeLensEntryKind.DefinitionMethod);
        if (!seen.Add(key)) return;

        var paramTypes = BuildParamTypes(symbol.Parameters);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionMethod,
            MethodName = ".ctor",
            TypeShortName = symbol.ContainingType.Name,
            ReturnType = null,
            ArgTypes = paramTypes.Count > 0 ? paramTypes.Select(p => p.TypeName).ToList() : null,
            ArgNames = paramTypes.Count > 0 ? paramTypes.Select(p => p.ParamName).ToList() : null,
            ArgCount = symbol.Parameters.Length,
            SourceLine = CollectorHelpers.GetSourceLine(model, node.Identifier.SpanStart),
        });
    }

    internal static void CollectType(
        TypeDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not INamedTypeSymbol symbol) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(symbol);
        var key = new DefinitionKey(typeName, CodeLensEntryKind.DefinitionType);
        if (!seen.Add(key)) return;

        var baseTypes = new List<string>();
        if (node.BaseList != null)
        {
            foreach (var baseTypeSyntax in node.BaseList.Types)
            {
                if (model.GetTypeInfo(baseTypeSyntax.Type).Type is INamedTypeSymbol baseSymbol)
                {
                    var baseFullName = CodeLensTypeFormatter.GetDisplayName(baseSymbol);
                    if (!CodeLensTypeChecker.IsTrivialDisplayName(baseFullName))
                        baseTypes.Add(baseFullName);
                }
            }
        }

        List<string>? primaryCtorParams = null;
        List<string>? primaryCtorParamNames = null;
        var primaryCtorArgCount = 0;
        if (node.ParameterList is { Parameters.Count: > 0 })
        {
            var primaryCtor = symbol.InstanceConstructors
                .FirstOrDefault(c => c.Parameters.Length == node.ParameterList.Parameters.Count
                                     && !c.IsImplicitlyDeclared);
            if (primaryCtor != null)
            {
                var paramTypes = BuildParamTypes(primaryCtor.Parameters);
                primaryCtorArgCount = primaryCtor.Parameters.Length;
                if (paramTypes.Count > 0)
                {
                    primaryCtorParams = paramTypes.Select(p => p.TypeName).ToList();
                    primaryCtorParamNames = paramTypes.Select(p => p.ParamName).ToList();
                }
            }
        }

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionType,
            TypeFullName = typeName,
            TypeShortName = symbol.Name,
            BaseTypes = baseTypes.Count > 0 ? baseTypes : null,
            ArgTypes = primaryCtorParams,
            ArgNames = primaryCtorParamNames,
            ArgCount = primaryCtorArgCount,
            SourceLine = CollectorHelpers.GetSourceLine(model, node.Identifier.SpanStart),
        });
    }

    internal static void CollectField(
        FieldDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            if (model.GetDeclaredSymbol(variable) is not IFieldSymbol symbol) continue;
            if (CodeLensTypeChecker.IsTrivial(symbol.Type)) continue;

            var typeName = CodeLensTypeFormatter.GetDisplayName(symbol.Type);
            var key = new DefinitionKey(symbol.Name, CodeLensEntryKind.DefinitionField);
            if (!seen.Add(key)) continue;

            output.Add(new CodeLensEntry
            {
                Kind = CodeLensEntryKind.DefinitionField,
                VariableName = symbol.Name,
                ResolvedType = typeName,
                SourceLine = CollectorHelpers.GetSourceLine(model, variable.SpanStart),
            });
        }
    }

    internal static void CollectProperty(
        PropertyDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not IPropertySymbol symbol) return;
        if (CodeLensTypeChecker.IsTrivial(symbol.Type)) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(symbol.Type);
        var key = new DefinitionKey(symbol.Name, CodeLensEntryKind.DefinitionProperty);
        if (!seen.Add(key)) return;

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionProperty,
            VariableName = symbol.Name,
            ResolvedType = typeName,
            SourceLine = CollectorHelpers.GetSourceLine(model, node.Identifier.SpanStart),
        });
    }

    /// <summary>
    /// Collects the containing type (nearest enclosing TypeDeclarationSyntax) as a scope entry.
    /// </summary>
    internal static void CollectContainingType(
        INamedTypeSymbol typeSymbol,
        SemanticModel model,
        int declarationSpanStart,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        var typeName = CodeLensTypeFormatter.GetDisplayName(typeSymbol);
        var key = new DefinitionKey(typeName, CodeLensEntryKind.ContainingType);
        if (!seen.Add(key)) return;

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.ContainingType,
            TypeFullName = typeName,
            SourceLine = CollectorHelpers.GetSourceLine(model, declarationSpanStart),
        });
    }

    static List<(string TypeName, string ParamName)> BuildParamTypes(
        IEnumerable<IParameterSymbol> parameters)
        => parameters
            .Select(p => (TypeName: CodeLensTypeFormatter.GetDisplayName(p.Type), ParamName: p.Name))
            .ToList();
}
