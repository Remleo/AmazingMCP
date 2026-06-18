using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Services.UsageQuery.EntryFactories;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Entry point for creating <see cref="QueryEntry"/> instances from syntax nodes.
/// Delegates to specialized factories per node kind.
/// </summary>
public static class QueryEntryFactory
{
    public static IEnumerable<QueryEntry> TryCreate(SyntaxNode node, SemanticModel model)
    {
        var entry = node switch
        {
            InvocationExpressionSyntax invocation           => InvocationEntryFactory.FromInvocation(invocation, model),
            ObjectCreationExpressionSyntax ctor             => TypeExpressionEntryFactory.FromObjectCreation(ctor, model),
            ImplicitObjectCreationExpressionSyntax implCtor => TypeExpressionEntryFactory.FromImplicitObjectCreation(implCtor, model),
            MemberAccessExpressionSyntax memberAccess       => MemberAccessEntryFactory.FromMemberAccess(memberAccess, model),
            IdentifierNameSyntax identifier                 => IdentifierEntryFactory.FromIdentifier(identifier, model),
            GenericNameSyntax generic                       => TypeExpressionEntryFactory.FromGenericName(generic, model),
            TypeConstraintSyntax constraint                 => TypeExpressionEntryFactory.FromTypeConstraint(constraint, model),
            ParameterSyntax parameter                       => TypeExpressionEntryFactory.FromParameter(parameter, model),
            TypeOfExpressionSyntax typeOf                   => TypeExpressionEntryFactory.FromTypeOf(typeOf, model),
            BinaryExpressionSyntax binary                   => TypeExpressionEntryFactory.FromBinaryExpression(binary, model),
            AssignmentExpressionSyntax assignment           => MemberAccessEntryFactory.FromEventAssignment(assignment, model),
            _ => null,
        };

        if (entry is null) yield break;
        yield return entry;

        var extensionEntry = InvocationEntryFactory.TryCreateExtensionMethodEntry(node, entry, model);
        if (extensionEntry is not null)
            yield return extensionEntry;
    }

    internal static bool IsDeclarationContext(IdentifierNameSyntax node) => node.Parent switch
    {
        ExplicitInterfaceSpecifierSyntax => true,
        NameEqualsSyntax                 => true,
        _                                => false,
    };

    internal static bool IsWriteTarget(SyntaxNode node) => node.Parent switch
    {
        AssignmentExpressionSyntax assign when assign.Left == node                                    => true,
        AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax }                            => true,
        ArgumentSyntax arg when arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)                      => true,
        _                                                                                             => false,
    };

    internal static IReadOnlyList<string> GetArgumentTypes(ArgumentListSyntax? argList, SemanticModel model)
    {
        if (argList is null) return [];

        var result = new List<string>(argList.Arguments.Count);
        foreach (var arg in argList.Arguments)
        {
            var typeInfo = model.GetTypeInfo(arg.Expression);
            result.Add(typeInfo.Type?.ToDisplayString() ?? "?");
        }
        return result;
    }
}
