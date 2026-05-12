using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Resolves the nearest meaningful ancestor syntax node that defines
/// the code section containing a given usage node.
///
/// Algorithm: walk up the AST from the usage node.
/// - BlockSyntax encountered: measure the parent node's total span (block + keyword line).
///   If ≤ <see cref="SectionThreshold"/> lines → section is the parent (e.g. CatchClauseSyntax).
///   If > <see cref="SectionThreshold"/> lines → stop, return usage node span only.
/// - Any other qualifying section ancestor found first → return it.
/// - Root reached without finding either → return usage node span.
/// </summary>
public static class SectionResolver
{
    /// <summary>
    /// Maximum line count of a block's parent node to be considered a displayable section.
    /// Covers the keyword line(s) + block body (e.g. "catch (...)\n{\n  ...\n}").
    /// </summary>
    const int SectionThreshold = 8;

    public static ScopeSection Resolve(SyntaxNode usageNode)
    {
        var current = usageNode.Parent;
        while (current is not null)
        {
            if (current is BlockSyntax block)
            {
                var parent = block.Parent;
                if (parent is null)
                    return ToSection(usageNode);

                // Measure the parent span — it includes the keyword line(s) before the block.
                var parentSpan = parent.GetLocation().GetLineSpan();
                var parentLines = parentSpan.EndLinePosition.Line - parentSpan.StartLinePosition.Line + 1;

                // When coming via a block, always use the full parent span (not just condition).
                // The block is compact enough — show the whole thing including the body.
                return parentLines <= SectionThreshold
                    ? ToSectionFull(parent)
                    : ToSection(usageNode);
            }

            if (IsSection(current, usageNode))
                return ToSection(current);

            current = current.Parent;
        }

        return ToSection(usageNode);
    }

    static bool IsSection(SyntaxNode node, SyntaxNode usageNode) => node switch
    {
        InvocationExpressionSyntax             => true,
        ObjectCreationExpressionSyntax         => true,
        ImplicitObjectCreationExpressionSyntax => true,
        AssignmentExpressionSyntax assign      =>
            // Skip assignments inside object/collection initializers — the walk continues
            // up to the containing ObjectCreationExpression which is the meaningful section.
            assign.Parent is not InitializerExpressionSyntax,
        LocalDeclarationStatementSyntax        => true,
        FieldDeclarationSyntax                 => true,
        ReturnStatementSyntax                  => true,
        YieldStatementSyntax                   => true,
        ThrowStatementSyntax                   => true,
        ThrowExpressionSyntax                  => true,
        ConditionalExpressionSyntax            => true,
        SwitchExpressionSyntax                 => true,
        SwitchSectionSyntax                    => true,
        AttributeSyntax                        => true,
        ParameterSyntax                        => true,
        PropertyDeclarationSyntax              => true,
        ForStatementSyntax s                   => !s.Statement.Contains(usageNode),
        ForEachStatementSyntax                 => true,
        IfStatementSyntax s                    => !s.Statement.Contains(usageNode) && !(s.Else?.Contains(usageNode) ?? false),
        WhileStatementSyntax s                 => !s.Statement.Contains(usageNode),
        _ => false
    };

    /// <summary>
    /// Returns a section spanning the full node — used when arriving via a compact block,
    /// where the entire parent (including body) should be shown.
    /// </summary>
    static ScopeSection ToSectionFull(SyntaxNode node)
    {
        // For ParameterSyntax in primary constructor — still span the parameter list
        SyntaxNode spanNode = node is ParameterSyntax param && IsInPrimaryConstructor(param)
            ? param.Parent!
            : node;

        var span = spanNode.GetLocation().GetLineSpan();
        var start = span.StartLinePosition.Line + 1;
        var end   = span.EndLinePosition.Line + 1;
        return new ScopeSection(node, start, end);
    }

    static ScopeSection ToSection(SyntaxNode node)
    {
        SyntaxNode spanNode = node switch
        {
            IfStatementSyntax ifStmt       => ifStmt.Condition,
            WhileStatementSyntax whileStmt => whileStmt.Condition,
            PropertyDeclarationSyntax prop => prop.Type,
            ParameterSyntax param when IsInPrimaryConstructor(param) => param.Parent!,
            _ => node
        };

        var span = spanNode.GetLocation().GetLineSpan();
        var start = span.StartLinePosition.Line + 1;
        var end   = span.EndLinePosition.Line + 1;
        return new ScopeSection(node, start, end);
    }

    static bool IsInPrimaryConstructor(ParameterSyntax param) =>
        param.Parent?.Parent is TypeDeclarationSyntax;
}
