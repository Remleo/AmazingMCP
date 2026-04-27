using AmazingMCP.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

/// <summary>
/// Resolves the <see cref="LineRange"/> of a method/property/operator declaration signature,
/// spanning from the first line of the declaration to the opening brace (or arrow for expression bodies).
/// Also provides the full range including the body via <see cref="ResolveFullRange"/>.
/// </summary>
public static class MethodDefinitionRangeResolver
{
    public static LineRange Resolve(MethodDeclarationSyntax node)
    {
        var start = StartLine(node);
        int end;
        if (node.Body is not null)
            end = BraceLine(node.Body.OpenBraceToken);
        else if (node.ExpressionBody is not null)
            end = StartLine(node.ExpressionBody);
        else
            end = EndLine(node);
        return new LineRange(start, end);
    }

    public static LineRange ResolveFullRange(MethodDeclarationSyntax node) =>
        new(StartLine(node), EndLine(node));

    public static LineRange Resolve(ConstructorDeclarationSyntax node)
    {
        var start = StartLine(node);
        int end;
        if (node.Body is not null)
            end = BraceLine(node.Body.OpenBraceToken);
        else if (node.ExpressionBody is not null)
            end = StartLine(node.ExpressionBody);
        else
            end = EndLine(node);
        return new LineRange(start, end);
    }

    public static LineRange ResolveFullRange(ConstructorDeclarationSyntax node) =>
        new(StartLine(node), EndLine(node));

    public static LineRange Resolve(PropertyDeclarationSyntax node)
    {
        var start = StartLine(node);
        int end;
        if (node.AccessorList is not null)
            end = BraceLine(node.AccessorList.OpenBraceToken);
        else if (node.ExpressionBody is not null)
            end = StartLine(node.ExpressionBody);
        else
            end = EndLine(node);
        return new LineRange(start, end);
    }

    public static LineRange ResolveFullRange(PropertyDeclarationSyntax node) =>
        new(StartLine(node), EndLine(node));

    public static LineRange Resolve(OperatorDeclarationSyntax node)
    {
        var start = StartLine(node);
        var end = node.Body is not null ? BraceLine(node.Body.OpenBraceToken) : EndLine(node);
        return new LineRange(start, end);
    }

    public static LineRange ResolveFullRange(OperatorDeclarationSyntax node) =>
        new(StartLine(node), EndLine(node));

    public static LineRange Resolve(ConversionOperatorDeclarationSyntax node)
    {
        var start = StartLine(node);
        var end = node.Body is not null ? BraceLine(node.Body.OpenBraceToken) : EndLine(node);
        return new LineRange(start, end);
    }

    public static LineRange ResolveFullRange(ConversionOperatorDeclarationSyntax node) =>
        new(StartLine(node), EndLine(node));

    // ── Helpers ───────────────────────────────────────────────────────────────

    static int StartLine(Microsoft.CodeAnalysis.SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    static int EndLine(Microsoft.CodeAnalysis.SyntaxNode node) =>
        node.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

    static int BraceLine(Microsoft.CodeAnalysis.SyntaxToken token) =>
        token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
