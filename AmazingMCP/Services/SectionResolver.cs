using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

/// <summary>
/// Resolves the nearest meaningful ancestor syntax node that defines
/// the code section containing a given usage node.
/// </summary>
public static class SectionResolver
{
    /// <summary>
    /// Walks up the syntax tree from <paramref name="node"/> and returns
    /// the nearest ancestor that qualifies as a displayable section.
    /// Falls back to a single-line section at the node itself.
    /// </summary>
    public static ScopeSection Resolve(SyntaxNode usageNode)
    {
        var current = usageNode.Parent;
        while (current is not null)
        {
            if (IsSection(current, usageNode))
                return ToSection(current);

            current = current.Parent;
        }

        return ToSection(usageNode);
    }

    /// <summary>
    /// Returns a single-line section at the usage node itself, bypassing ancestor resolution.
    /// Used when the usage is inside a large block where section context is suppressed.
    /// </summary>
    public static ScopeSection ResolveFallback(SyntaxNode usageNode) => ToSection(usageNode);

    static bool IsSection(SyntaxNode node, SyntaxNode usageNode) => node switch
    {
        InvocationExpressionSyntax      => true,
        ObjectCreationExpressionSyntax  => true,
        ImplicitObjectCreationExpressionSyntax => true,
        AssignmentExpressionSyntax      => true,
        LocalDeclarationStatementSyntax => true,
        FieldDeclarationSyntax          => true,
        ReturnStatementSyntax           => true,
        YieldStatementSyntax            => true,
        ThrowStatementSyntax            => true,
        ThrowExpressionSyntax           => true,
        ConditionalExpressionSyntax     => true,
        SwitchExpressionSyntax          => true,
        SwitchSectionSyntax             => true,
        AttributeSyntax                 => true,
        ParameterSyntax                 => true,
        PropertyDeclarationSyntax       => true,
        ForStatementSyntax s            => !s.Statement.Contains(usageNode),
        ForEachStatementSyntax          => true,
        // if/while: only when usage is in the condition, not in the body
        IfStatementSyntax s             => !s.Statement.Contains(usageNode) && !(s.Else?.Contains(usageNode) ?? false),
        WhileStatementSyntax s          => !s.Statement.Contains(usageNode),
        _ => false
    };

    static ScopeSection ToSection(SyntaxNode node)
    {
        SyntaxNode spanNode = node switch
        {
            IfStatementSyntax ifStmt         => ifStmt.Condition,
            WhileStatementSyntax whileStmt   => whileStmt.Condition,
            PropertyDeclarationSyntax prop   => prop.Type,
            // For a parameter in a primary constructor — span the entire parameter list
            ParameterSyntax param when IsInPrimaryConstructor(param) => param.Parent!,
            _                                => node
        };

        var span = spanNode.GetLocation().GetLineSpan();
        var start = span.StartLinePosition.Line + 1;
        var end   = span.EndLinePosition.Line + 1;
        return new ScopeSection(node, start, end);
    }

    static bool IsInPrimaryConstructor(ParameterSyntax param) =>
        param.Parent?.Parent is TypeDeclarationSyntax;
}
