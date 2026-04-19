using System.Text.RegularExpressions;
using System.Xml.Linq;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services;

public partial class AbstractionExtractor : IAbstractionExtractor
{
    public AbstractionInfo BuildAbstractionInfo(
        RawTypeInfo typeInfo,
        string projectName,
        IReadOnlyList<string> implementations)
    {
        return new AbstractionInfo
        {
            FullName = typeInfo.FullName,
            Namespace = typeInfo.Namespace,
            ProjectName = projectName,
            SourceFilePath = typeInfo.SourceFilePath,
            IsInterface = typeInfo.IsInterface,
            IsAbstractClass = typeInfo.IsAbstractClass,
            IsStaticClass = typeInfo.IsStaticClass,
            Implementations = implementations,
            OpenGenericFullName = typeInfo.OpenGenericFullName
        };
    }

    public AbstractionInfo BuildAbstractionInfo(
        INamedTypeSymbol symbol,
        string projectName,
        IReadOnlyList<string> implementations)
    {
        var typeInfo = RawTypeInfo.From(symbol);
        var summary = ExtractXmlDocSummary(symbol);
        return new AbstractionInfo
        {
            FullName = typeInfo.FullName,
            Namespace = typeInfo.Namespace,
            ProjectName = projectName,
            SourceFilePath = typeInfo.SourceFilePath,
            IsInterface = typeInfo.IsInterface,
            IsAbstractClass = typeInfo.IsAbstractClass,
            IsStaticClass = typeInfo.IsStaticClass,
            Implementations = implementations,
            OpenGenericFullName = typeInfo.OpenGenericFullName,
            XmlDocSummary = summary
        };
    }

    /// <summary>
    /// Extracts the &lt;summary&gt; text from the symbol's XML documentation comment.
    /// Returns null if no summary is present. Full text is preserved without truncation.
    /// </summary>
    internal static string? ExtractXmlDocSummary(ISymbol symbol)
        => ExtractXmlDocSummary(symbol.GetDocumentationCommentXml());

    /// <summary>
    /// Extracts the &lt;summary&gt; text from a raw XML documentation comment string.
    /// Returns null if no summary is present. Full text is preserved without truncation.
    /// </summary>
    internal static string? ExtractXmlDocSummary(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var doc = XDocument.Parse(xml);
            var summaryEl = doc.Descendants("summary").FirstOrDefault();
            if (summaryEl is null)
                return null;

            // Collapse whitespace: trim each line, join with single space
            var text = WhitespaceRegex().Replace(summaryEl.Value, " ").Trim();
            if (string.IsNullOrEmpty(text))
                return null;

            return text;
        }
        catch
        {
            return null;
        }
    }

    public INamedTypeSymbol? FindClosedGenericInterface(string ifaceName, List<SourceType> classes)
    {
        foreach (var entry in classes)
            foreach (var iface in entry.Symbol.AllInterfaces)
                if (iface.ToDisplayString() == ifaceName)
                    return iface;
        return null;
    }

    public string ResolveProjectForClosedGeneric(
        INamedTypeSymbol closedGenericSymbol, List<SourceType> allTypes)
    {
        var originalDefName = closedGenericSymbol.OriginalDefinition.ToDisplayString();

        return allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Interface
                        && t.Symbol.ToDisplayString() == originalDefName)
            .OrderBy(t =>
            {
                var path = t.Symbol.DeclaringSyntaxReferences
                    .FirstOrDefault()?.SyntaxTree.FilePath ?? "";
                return path.Contains(t.ProjectName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            })
            .FirstOrDefault()?.ProjectName ?? "";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
