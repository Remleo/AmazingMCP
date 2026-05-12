using AmazingMCP.Models;
using AmazingMCP.Models.CodeLens;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AmazingMCP.Services.CodeLens;

/// <summary>
/// Dispatches Roslyn syntax nodes to the appropriate sub-collector.
/// </summary>
public static class CodeLensCollector
{
    public static void Collect(
        SyntaxNode node,
        SemanticModel model,
        TextSpan span,
        INamedTypeSymbol? containingType,
        HashSet<VariableKey> seenVariables,
        HashSet<CallKey> seenCalls,
        HashSet<ExtensionKey> seenExtensions,
        HashSet<ConstructorKey> seenConstructors,
        HashSet<FieldKey> seenFields,
        HashSet<PropertyKey> seenProperties,
        HashSet<DefinitionKey> seenDefinitions,
        List<CodeLensEntry> variables,
        List<CodeLensEntry> calls,
        List<CodeLensEntry> extensions,
        List<CodeLensEntry> constructors,
        List<CodeLensEntry> fields,
        List<CodeLensEntry> properties,
        List<CodeLensEntry> definitionMethods,
        List<CodeLensEntry> definitionTypes)
    {
        switch (node)
        {
            case IdentifierNameSyntax identifier:
                // Try local variable read first, then field/property (implicit this)
                VariableCollector.CollectIdentifierUsage(identifier, model, seenVariables, variables);
                MemberAccessCollector.CollectIdentifier(identifier, model, containingType, seenFields, seenProperties, fields, properties);
                break;

            case InvocationExpressionSyntax invocation:
                InvocationCollector.Collect(invocation, model, containingType, seenCalls, seenExtensions, calls, extensions);
                break;

            case ObjectCreationExpressionSyntax objCreation:
                ObjectCreationCollector.Collect(objCreation, model, seenConstructors, constructors);
                break;

            case ImplicitObjectCreationExpressionSyntax implicitCreation:
                ObjectCreationCollector.CollectImplicit(implicitCreation, model, seenConstructors, constructors);
                break;

            case MemberAccessExpressionSyntax memberAccess:
                MemberAccessCollector.CollectMemberAccess(memberAccess, model, containingType, seenFields, seenProperties, fields, properties);
                break;

            // Definition nodes: only collect when the declaration identifier starts within the span.
            // Without this guard, a class/method declared above the requested range would be
            // included because its full span (entire body) overlaps with any inner range.
            case MethodDeclarationSyntax methodDecl when span.Contains(methodDecl.Identifier.SpanStart):
                DefinitionCollector.CollectMethod(methodDecl, model, seenDefinitions, definitionMethods);
                break;

            case ConstructorDeclarationSyntax ctorDecl when span.Contains(ctorDecl.Identifier.SpanStart):
                DefinitionCollector.CollectConstructor(ctorDecl, model, seenDefinitions, definitionMethods);
                break;

            case FieldDeclarationSyntax fieldDecl when span.Contains(fieldDecl.SpanStart):
                DefinitionCollector.CollectField(fieldDecl, model, seenDefinitions, definitionTypes);
                break;

            case PropertyDeclarationSyntax propDecl when span.Contains(propDecl.Identifier.SpanStart):
                DefinitionCollector.CollectProperty(propDecl, model, seenDefinitions, definitionTypes);
                break;

            case ClassDeclarationSyntax classDecl when span.Contains(classDecl.Identifier.SpanStart):
                DefinitionCollector.CollectType(classDecl, model, seenDefinitions, definitionTypes);
                break;

            case InterfaceDeclarationSyntax ifaceDecl when span.Contains(ifaceDecl.Identifier.SpanStart):
                DefinitionCollector.CollectType(ifaceDecl, model, seenDefinitions, definitionTypes);
                break;

            case RecordDeclarationSyntax recordDecl when span.Contains(recordDecl.Identifier.SpanStart):
                DefinitionCollector.CollectType(recordDecl, model, seenDefinitions, definitionTypes);
                break;

            case StructDeclarationSyntax structDecl when span.Contains(structDecl.Identifier.SpanStart):
                DefinitionCollector.CollectType(structDecl, model, seenDefinitions, definitionTypes);
                break;
        }
    }
}
