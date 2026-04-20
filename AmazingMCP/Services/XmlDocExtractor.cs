using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

/// Extracts and renders XML doc-comment summaries from Roslyn syntax nodes.
internal static partial class XmlDocExtractor
{
    internal static void AppendXmlDoc(SyntaxNode node, StringBuilder sb, int indent)
    {
        var summary = ExtractSummary(node);
        if (summary is null) return;
        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/// {summary}");
    }

    internal static string? ExtractSummary(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                              || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (trivia == default) return null;

        var xml = trivia.GetStructure();
        if (xml is null) return null;

        var summaryElement = xml.DescendantNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.LocalName.Text == "summary");

        if (summaryElement is null) return null;

        var text = string.Concat(
            summaryElement.Content
                .Select(c => c switch
                {
                    XmlTextSyntax t => string.Concat(t.TextTokens.Select(tok => tok.ValueText)),
                    _ => ""
                }));

        text = WhitespaceRegex().Replace(text.Trim(), " ");

        if (string.IsNullOrWhiteSpace(text)) return null;

        return text.Length > 200 ? text[..200] + "…" : text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
