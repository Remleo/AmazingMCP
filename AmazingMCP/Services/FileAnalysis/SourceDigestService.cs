using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.FileAnalysis;

public class SourceDigestService(IXmlDocExtractor xmlDoc) : ISourceDigestService
{
    public string GetDigest(string source, bool includeLineNumbers)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var sb   = new StringBuilder();
        AppendUsings(root, sb, includeLineNumbers);
        WalkNodes(root.ChildNodes(), sb, indent: 0, includeLineNumbers);
        return sb.ToString().TrimEnd();
    }

    static void AppendUsings(SyntaxNode root, StringBuilder sb, bool includeLineNumbers)
    {
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        if (usings.Count == 0) return;

        var startLine = usings[0].GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine   = usings[^1].GetLocation().GetLineSpan().EndLinePosition.Line + 1;
        var lines     = endLine - startLine;
        var pos       = lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";

        sb.AppendLine(includeLineNumbers ? $"/*{pos}*/ usings" : "usings");
    }

    void WalkNodes(IEnumerable<SyntaxNode> nodes, StringBuilder sb, int indent, bool includeLineNumbers)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case FileScopedNamespaceDeclarationSyntax fsns:
                    sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent), SyntaxNodeFormatter.Pos(fsns), SyntaxNodeFormatter.Sig(fsns), includeLineNumbers));
                    WalkNodes(fsns.Members, sb, indent, includeLineNumbers);
                    break;

                case NamespaceDeclarationSyntax ns:
                    sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent), SyntaxNodeFormatter.Pos(ns), SyntaxNodeFormatter.Sig(ns), includeLineNumbers));
                    WalkNodes(ns.Members, sb, indent + 1, includeLineNumbers);
                    break;

                case TypeDeclarationSyntax type:
                    foreach (var a in type.AttributeLists)
                        sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent), SyntaxNodeFormatter.Pos(a), a.ToString().Trim(), includeLineNumbers));
                    if (xmlDoc.ExtractDocDigest(type) is { } typeDoc)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/// {typeDoc}");
                    sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent), SyntaxNodeFormatter.Pos(type), SyntaxNodeFormatter.Sig(type), includeLineNumbers));
                    WalkNodes(type.Members, sb, indent + 1, includeLineNumbers);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    foreach (var a in enumDecl.AttributeLists)
                        sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent), SyntaxNodeFormatter.Pos(a), a.ToString().Trim(), includeLineNumbers));
                    if (xmlDoc.ExtractDocDigest(enumDecl) is { } enumDoc)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/// {enumDoc}");
                    sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent), SyntaxNodeFormatter.Pos(enumDecl), SyntaxNodeFormatter.Sig(enumDecl), includeLineNumbers));
                    foreach (var member in enumDecl.Members)
                        sb.AppendLine(FormatLine(SyntaxNodeFormatter.Pad(indent + 1), SyntaxNodeFormatter.Pos(member), member.ToString().Trim(), includeLineNumbers));
                    break;

                case MemberDeclarationSyntax member:
                    AppendMember(member, sb, indent, includeLineNumbers);
                    break;
            }
        }
    }

    void AppendMember(MemberDeclarationSyntax member, StringBuilder sb, int indent, bool includeLineNumbers)
    {
        var sig = MemberSignatureExtractor.GetSignature(member);
        if (string.IsNullOrWhiteSpace(sig)) return;

        sig = MemberSignatureExtractor.StripLeadingAttributes(member, sig);
        if (string.IsNullOrWhiteSpace(sig)) return;

        if (xmlDoc.ExtractDocDigest(member) is { } memberDoc)
            sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/// {memberDoc}");

        foreach (var attrList in member.AttributeLists)
            sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}{attrList.ToString().Trim()}");

        var sigLines = sig.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var pad      = SyntaxNodeFormatter.Pad(indent);
        sb.Append(FormatLine(pad, SyntaxNodeFormatter.PosWithLeadingTrivia(member), sigLines[0], includeLineNumbers));
        for (var i = 1; i < sigLines.Length; i++)
        {
            sb.AppendLine();
            sb.Append($"{pad}{sigLines[i]}");
        }
        sb.AppendLine();
    }

    static string FormatLine(string pad, string pos, string content, bool includeLineNumbers) =>
        includeLineNumbers ? $"{pad}/*{pos}*/ {content}" : $"{pad}{content}";
}
