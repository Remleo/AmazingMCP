using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

/// Extracts and renders XML doc-comment summaries from Roslyn syntax nodes.
public partial class XmlDocExtractor : IXmlDocExtractor
{
    public string? ExtractDocDigest(SyntaxNode node)
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

    public string? ExtractSymbolDoc(ISymbol symbol, string prefix)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return null;

        var inner = MemberTagRegex().Replace(xml.Trim(), "$1").Trim();
        if (string.IsNullOrWhiteSpace(inner)) return null;

        var lines = inner.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var formatted = lines
            .Select(l => l.TrimStart())
            .Where(l => l.Length > 0)
            .Select(l => $"{prefix}/// {l}");
        return string.Join("\n", formatted);
    }

    [GeneratedRegex(@"^\s*<member[^>]*>(.*)</member>\s*$", RegexOptions.Singleline)]
    private partial Regex MemberTagRegex();

    [GeneratedRegex(@"\s+")]
    private partial Regex WhitespaceRegex();
}
