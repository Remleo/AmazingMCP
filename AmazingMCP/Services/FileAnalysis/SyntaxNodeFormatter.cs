using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.FileAnalysis;

/// Formatting helpers: position strings, signatures, indentation, whitespace normalization.
internal static partial class SyntaxNodeFormatter
{
    // ── position ───────────────────────────────────────────────────────────────

    internal static string Pos(SyntaxNode node)
    {
        var span      = node.GetLocation().GetLineSpan();
        var startLine = span.StartLinePosition.Line + 1;
        var endLine   = node is FileScopedNamespaceDeclarationSyntax fsns
            ? fsns.SemicolonToken.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            : span.EndLinePosition.Line + 1;
        var lines     = endLine - startLine;

        return lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";
    }

    /// Like Pos, but startLine includes leading xmldoc/attribute trivia.
    internal static string PosWithLeadingTrivia(SyntaxNode node)
    {
        var span      = node.GetLocation().GetLineSpan();
        var endLine   = span.EndLinePosition.Line + 1;
        var startLine = LeadingTriviaStartLine(node) + 1;
        var lines     = endLine - startLine;

        return lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";
    }

    /// Returns the first line of leading doc-comment or attribute trivia, or the node's own start line.
    internal static int LeadingTriviaStartLine(SyntaxNode node)
    {
        var leading = node.GetLeadingTrivia();
        foreach (var trivia in leading)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
             || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                return trivia.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        if (node is MemberDeclarationSyntax memberDecl && memberDecl.AttributeLists.Count > 0)
            return memberDecl.AttributeLists[0].GetLocation().GetLineSpan().StartLinePosition.Line;

        return node.GetLocation().GetLineSpan().StartLinePosition.Line;
    }

    // ── signature extraction ───────────────────────────────────────────────────

    /// Namespace / type header: everything up to (not including) the opening brace or semicolon.
    internal static string Sig(SyntaxNode node)
    {
        var text      = node.ToString();
        var nodeStart = node.Span.Start;

        var tokenStart = node switch
        {
            FileScopedNamespaceDeclarationSyntax fsns => fsns.SemicolonToken.Span.Start,
            NamespaceDeclarationSyntax ns             => ns.OpenBraceToken.Span.Start,
            TypeDeclarationSyntax t                   => t.OpenBraceToken.Span.Start,
            EnumDeclarationSyntax e                   => e.OpenBraceToken.Span.Start,
            _                                         => -1
        };

        if (tokenStart < 0) return NormalizeWhitespace(text);

        var relIdx = tokenStart - nodeStart;
        if (relIdx <= 0 || relIdx > text.Length) return NormalizeWhitespace(text);

        return NormalizeWhitespace(text[..relIdx]);
    }

    // ── indentation ────────────────────────────────────────────────────────────

    internal static string Pad(int indent) => new(' ', indent * 4);

    // ── whitespace normalization ───────────────────────────────────────────────

    /// Collapse all whitespace/newlines into a single space.
    internal static string NormalizeWhitespace(string s) =>
        WhitespaceRegex().Replace(s.Trim(), " ");

    /// Normalize inline whitespace per line, preserving relative leading indentation.
    internal static string NormalizeWhitespacePreserveIndent(string s)
    {
        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        int baseIndent = 0;
        foreach (var l in lines)
        {
            if (string.IsNullOrWhiteSpace(l)) continue;
            baseIndent = l.Length - l.TrimStart().Length;
            break;
        }

        var result = new StringBuilder();
        bool first = true;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (!first) result.AppendLine();
                continue;
            }

            int lineIndent = line.Length - line.TrimStart().Length;
            int relIndent  = Math.Max(0, lineIndent - baseIndent);
            var trimmed    = InlineWhitespaceRegex().Replace(line.TrimStart(), " ");

            if (!first) result.AppendLine();
            result.Append(new string(' ', relIndent));
            result.Append(trimmed);
            first = false;
        }
        return result.ToString();
    }

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
