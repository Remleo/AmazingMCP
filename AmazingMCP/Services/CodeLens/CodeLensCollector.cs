using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.CodeLens;

/// <summary>
/// Walks Roslyn syntax nodes within a span and collects <see cref="CodeLensEntry"/> items,
/// applying deduplication via per-kind HashSets of record keys.
/// </summary>
public static class CodeLensCollector
{
    public static void Collect(
        SyntaxNode node,
        SemanticModel model,
        HashSet<VariableKey> seenVariables,
        HashSet<CallKey> seenCalls,
        HashSet<ExtensionKey> seenExtensions,
        HashSet<ConstructorKey> seenConstructors,
        HashSet<DefinitionKey> seenDefinitions,
        List<CodeLensEntry> variables,
        List<CodeLensEntry> calls,
        List<CodeLensEntry> extensions,
        List<CodeLensEntry> constructors,
        List<CodeLensEntry> definitionMethods,
        List<CodeLensEntry> definitionTypes)
    {
        switch (node)
        {
            case VariableDeclaratorSyntax varDeclarator:
                CollectVariable(varDeclarator, model, seenVariables, variables);
                break;

            case InvocationExpressionSyntax invocation:
                CollectInvocation(invocation, model, seenCalls, seenExtensions, calls, extensions);
                break;

            case ObjectCreationExpressionSyntax objCreation:
                CollectConstructor(objCreation, model, seenConstructors, constructors);
                break;

            case ImplicitObjectCreationExpressionSyntax implicitCreation:
                CollectImplicitConstructor(implicitCreation, model, seenConstructors, constructors);
                break;

            case MethodDeclarationSyntax methodDecl:
                CollectMethodDefinition(methodDecl, model, seenDefinitions, definitionMethods);
                break;

            case ConstructorDeclarationSyntax ctorDecl:
                CollectConstructorDefinition(ctorDecl, model, seenDefinitions, definitionMethods);
                break;

            case ClassDeclarationSyntax classDecl:
                CollectTypeDefinition(classDecl, model, seenDefinitions, definitionTypes);
                break;

            case InterfaceDeclarationSyntax ifaceDecl:
                CollectTypeDefinition(ifaceDecl, model, seenDefinitions, definitionTypes);
                break;

            case RecordDeclarationSyntax recordDecl:
                CollectTypeDefinition(recordDecl, model, seenDefinitions, definitionTypes);
                break;

            case StructDeclarationSyntax structDecl:
                CollectTypeDefinition(structDecl, model, seenDefinitions, definitionTypes);
                break;
        }
    }

    // ── Variables ─────────────────────────────────────────────────────────

    private static void CollectVariable(
        VariableDeclaratorSyntax node,
        SemanticModel model,
        HashSet<VariableKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not ILocalSymbol symbol) return;
        if (CodeLensTypeChecker.IsTrivial(symbol.Type)) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(symbol.Type);
        var key = new VariableKey(symbol.Name, typeName);
        if (!seen.Add(key)) return;

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Variable,
            VariableName = symbol.Name,
            ResolvedType = typeName,
        });
    }

    // ── Invocations ───────────────────────────────────────────────────────

    private static void CollectInvocation(
        InvocationExpressionSyntax node,
        SemanticModel model,
        HashSet<CallKey> seenCalls,
        HashSet<ExtensionKey> seenExtensions,
        List<CodeLensEntry> calls,
        List<CodeLensEntry> extensions)
    {
        if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol method) return;

        var isExtension = method.IsExtensionMethod || method.ReducedFrom != null;

        if (isExtension)
            CollectExtension(method, seenExtensions, extensions);
        else
            CollectCall(method, seenCalls, calls);
    }

    private static void CollectCall(
        IMethodSymbol method,
        HashSet<CallKey> seen,
        List<CodeLensEntry> output)
    {
        var key = new CallKey(method.Name);
        if (!seen.Add(key)) return;

        var returnType = CodeLensTypeFormatter.GetDisplayName(method.ReturnType);
        var argTypes = GetNonTrivialArgTypes(method.Parameters);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Call,
            MethodName = method.Name,
            ReturnType = CodeLensTypeChecker.IsTrivialDisplayName(returnType) ? null : returnType,
            ArgTypes = argTypes.Count > 0 ? argTypes : null,
            ArgCount = method.Parameters.Length,
        });
    }

    private static void CollectExtension(
        IMethodSymbol method,
        HashSet<ExtensionKey> seen,
        List<CodeLensEntry> output)
    {
        var key = new ExtensionKey(method.Name);
        if (!seen.Add(key)) return;

        // Original (non-reduced) method: first param is the receiver
        var original = method.ReducedFrom ?? method;
        var receiverType = CodeLensTypeFormatter.GetDisplayName(original.Parameters[0].Type);
        var extParams = original.Parameters.Skip(1).ToArray();
        var argTypes = GetNonTrivialArgTypes(extParams);
        var returnType = CodeLensTypeFormatter.GetDisplayName(method.ReturnType);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Extension,
            MethodName = method.Name,
            ReturnType = CodeLensTypeChecker.IsTrivialDisplayName(returnType) ? null : returnType,
            ArgTypes = argTypes.Count > 0 ? argTypes : null,
            ArgCount = extParams.Length,
            ReceiverType = CodeLensTypeChecker.IsTrivialDisplayName(receiverType) ? null : receiverType,
        });
    }

    // ── Constructors ──────────────────────────────────────────────────────

    private static void CollectConstructor(
        ObjectCreationExpressionSyntax node,
        SemanticModel model,
        HashSet<ConstructorKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetTypeInfo(node).Type is not INamedTypeSymbol namedType) return;
        AddConstructorEntry(namedType, model.GetSymbolInfo(node).Symbol as IMethodSymbol, seen, output);
    }

    private static void CollectImplicitConstructor(
        ImplicitObjectCreationExpressionSyntax node,
        SemanticModel model,
        HashSet<ConstructorKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetTypeInfo(node).Type is not INamedTypeSymbol namedType) return;
        AddConstructorEntry(namedType, model.GetSymbolInfo(node).Symbol as IMethodSymbol, seen, output);
    }

    private static void AddConstructorEntry(
        INamedTypeSymbol namedType,
        IMethodSymbol? ctor,
        HashSet<ConstructorKey> seen,
        List<CodeLensEntry> output)
    {
        var typeName = CodeLensTypeFormatter.GetDisplayName(namedType);
        if (CodeLensTypeChecker.IsTrivialDisplayName(typeName)) return;

        var key = new ConstructorKey(typeName);
        if (!seen.Add(key)) return;

        var argTypes = ctor != null ? GetNonTrivialArgTypes(ctor.Parameters) : [];
        var argCount = ctor?.Parameters.Length ?? 0;

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Constructor,
            TypeFullName = typeName,
            ArgTypes = argTypes.Count > 0 ? argTypes : null,
            ArgCount = argCount,
        });
    }

    // ── Definitions ───────────────────────────────────────────────────────

    private static void CollectMethodDefinition(
        MethodDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not IMethodSymbol symbol) return;

        var key = new DefinitionKey(symbol.Name, CodeLensEntryKind.DefinitionMethod);
        if (!seen.Add(key)) return;

        var returnType = CodeLensTypeFormatter.GetDisplayName(symbol.ReturnType);
        var paramTypes = GetNonTrivialArgTypes(symbol.Parameters);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionMethod,
            MethodName = symbol.Name,
            ReturnType = CodeLensTypeChecker.IsTrivialDisplayName(returnType) ? null : returnType,
            ArgTypes = paramTypes.Count > 0 ? paramTypes : null,
            ArgCount = symbol.Parameters.Length,
        });
    }

    private static void CollectConstructorDefinition(
        ConstructorDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not IMethodSymbol symbol) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(symbol.ContainingType);
        var key = new DefinitionKey(typeName, CodeLensEntryKind.DefinitionMethod);
        if (!seen.Add(key)) return;

        var paramTypes = GetNonTrivialArgTypes(symbol.Parameters);

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionMethod,
            MethodName = $"new {symbol.ContainingType.Name}",
            ReturnType = null,
            ArgTypes = paramTypes.Count > 0 ? paramTypes : null,
            ArgCount = symbol.Parameters.Length,
        });
    }

    private static void CollectTypeDefinition(
        TypeDeclarationSyntax node,
        SemanticModel model,
        HashSet<DefinitionKey> seen,
        List<CodeLensEntry> output)
    {
        if (model.GetDeclaredSymbol(node) is not INamedTypeSymbol symbol) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(symbol);
        var key = new DefinitionKey(typeName, CodeLensEntryKind.DefinitionType);
        if (!seen.Add(key)) return;

        // Only base types / interfaces explicitly listed in the syntax (visible in span)
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

        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.DefinitionType,
            TypeFullName = typeName,
            BaseTypes = baseTypes.Count > 0 ? baseTypes : null,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<string> GetNonTrivialArgTypes(IEnumerable<IParameterSymbol> parameters)
        => parameters
            .Select(p => CodeLensTypeFormatter.GetDisplayName(p.Type))
            .Where(t => !CodeLensTypeChecker.IsTrivialDisplayName(t))
            .ToList();
}
