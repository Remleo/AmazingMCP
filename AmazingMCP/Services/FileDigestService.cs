using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public class FileDigestService(IFileReader fileReader, IXmlDocExtractor xmlDoc) : IFileDigestService
{
    public string GetStructure(string filePath)
    {
        filePath = Path.GetFullPath(filePath);

        if (!fileReader.Exists(filePath))
            return $"File not found: {filePath}";

        var source = fileReader.ReadAllText(filePath);
        var root   = CSharpSyntaxTree.ParseText(source, path: filePath).GetRoot();
        var sb     = new StringBuilder();
        AppendUsings(root, sb);
        WalkNodes(root.ChildNodes(), sb, indent: 0);
        return sb.ToString().TrimEnd();
    }

    static void AppendUsings(SyntaxNode root, StringBuilder sb)
    {
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        if (usings.Count == 0) return;

        var startLine = usings[0].GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine   = usings[^1].GetLocation().GetLineSpan().EndLinePosition.Line + 1;
        var lines     = endLine - startLine;
        var pos       = lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";

        sb.AppendLine($"/*{pos}*/ usings");
    }

    void WalkNodes(IEnumerable<SyntaxNode> nodes, StringBuilder sb, int indent)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case FileScopedNamespaceDeclarationSyntax fsns:
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(fsns)}*/ {SyntaxNodeFormatter.Sig(fsns)}");
                    WalkNodes(fsns.Members, sb, indent);
                    break;

                case NamespaceDeclarationSyntax ns:
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(ns)}*/ {SyntaxNodeFormatter.Sig(ns)}");
                    WalkNodes(ns.Members, sb, indent + 1);
                    break;

                case TypeDeclarationSyntax type:
                    foreach (var a in type.AttributeLists)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(a)}*/ {a.ToString().Trim()}");
                    if (xmlDoc.ExtractDocDigest(type) is { } typeDoc)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/// {typeDoc}");
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(type)}*/ {SyntaxNodeFormatter.Sig(type)}");
                    WalkNodes(type.Members, sb, indent + 1);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    foreach (var a in enumDecl.AttributeLists)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(a)}*/ {a.ToString().Trim()}");
                    if (xmlDoc.ExtractDocDigest(enumDecl) is { } enumDoc)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/// {enumDoc}");
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(enumDecl)}*/ {SyntaxNodeFormatter.Sig(enumDecl)}");
                    foreach (var member in enumDecl.Members)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent + 1)}/*{SyntaxNodeFormatter.Pos(member)}*/ {member.ToString().Trim()}");
                    break;

                case MemberDeclarationSyntax member:
                    AppendMember(member, sb, indent);
                    break;
            }
        }
    }

    void AppendMember(MemberDeclarationSyntax member, StringBuilder sb, int indent)
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
        sb.Append($"{pad}/*{SyntaxNodeFormatter.PosWithLeadingTrivia(member)}*/ {sigLines[0]}");
        for (var i = 1; i < sigLines.Length; i++)
        {
            sb.AppendLine();
            sb.Append($"{pad}{sigLines[i]}");
        }
        sb.AppendLine();
    }
}
