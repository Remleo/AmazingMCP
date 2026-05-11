using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public interface IXmlDocExtractor
{
    /// Extracts the summary text from a source syntax node's doc comment.
    /// Returns null if no summary is present.
    string? ExtractDocDigest(SyntaxNode node);

    /// Formats the XML doc of a third-party ISymbol as /// lines.
    /// Strips the outer <member name="..."> wrapper and prefixes every line with "/// ".
    /// Returns null if no documentation is available.
    string? ExtractSymbolDoc(ISymbol symbol, string prefix);
}
