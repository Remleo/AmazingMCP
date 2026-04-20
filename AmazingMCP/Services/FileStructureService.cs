using System.Text;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public class FileStructureService
{
    // ── public API ─────────────────────────────────────────────────────────────

    public List<FileStructureItem> GetItems(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        if (!File.Exists(filePath)) return [];

        var root  = ParseRoot(filePath);
        var items = new List<FileStructureItem>();
        CollectUsingsItem(root, items);
        CollectNodes(root.ChildNodes(), items);
        return items;
    }

    public string GetStructure(string filePath)
    {
        filePath = Path.GetFullPath(filePath);

        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        var root = ParseRoot(filePath);
        var sb   = new StringBuilder();
        AppendUsings(root, sb);
        WalkNodes(root.ChildNodes(), sb, indent: 0);
        return sb.ToString().TrimEnd();
    }

    // ── parsing ────────────────────────────────────────────────────────────────

    static SyntaxNode ParseRoot(string filePath)
    {
        var source = File.ReadAllText(filePath);
        return CSharpSyntaxTree.ParseText(source, path: filePath).GetRoot();
    }

    // ── item collection ────────────────────────────────────────────────────────

    static void CollectUsingsItem(SyntaxNode root, List<FileStructureItem> items)
    {
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        if (usings.Count == 0) return;

        var startLine = usings[0].GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine   = usings[^1].GetLocation().GetLineSpan().EndLinePosition.Line + 1;

        items.Add(new FileStructureItem
        {
            SymbolString       = "usings",
            Kind               = FileStructureItemKind.Usings,
            StartLine          = startLine,
            EndLine            = endLine,
            DeclarationLine    = startLine,
            DeclarationEndLine = startLine
        });
    }

    static void CollectNodes(IEnumerable<SyntaxNode> nodes, List<FileStructureItem> items)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case FileScopedNamespaceDeclarationSyntax fsns:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(fsns), FileStructureItemKind.Namespace, fsns));
                    CollectNodes(fsns.Members, items);
                    break;

                case NamespaceDeclarationSyntax ns:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(ns), FileStructureItemKind.Namespace, ns));
                    CollectNodes(ns.Members, items);
                    break;

                case TypeDeclarationSyntax type:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(type), FileStructureItemKind.Type, type));
                    CollectNodes(type.Members, items);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(enumDecl), FileStructureItemKind.Type, enumDecl));
                    foreach (var member in enumDecl.Members)
                        items.Add(MakeItem(member.ToString().Trim(), FileStructureItemKind.Member, member));
                    break;

                case MemberDeclarationSyntax member:
                    CollectMemberItem(member, items);
                    break;
            }
        }
    }

    static void CollectMemberItem(MemberDeclarationSyntax member, List<FileStructureItem> items)
    {
        var sig = MemberSignatureExtractor.GetSignature(member);
        if (!string.IsNullOrWhiteSpace(sig))
            items.Add(MakeItem(sig, FileStructureItemKind.Member, member));
    }

    static FileStructureItem MakeItem(string symbolString, FileStructureItemKind kind, SyntaxNode node)
    {
        var nodeSpan  = node.GetLocation().GetLineSpan();
        var nodeEnd   = nodeSpan.EndLinePosition.Line + 1;
        var declLine  = nodeSpan.StartLinePosition.Line + 1;

        var declEndLine = node switch
        {
            TypeDeclarationSyntax t when t.OpenBraceToken != default
                => t.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            NamespaceDeclarationSyntax ns when ns.OpenBraceToken != default
                => ns.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            FileScopedNamespaceDeclarationSyntax fsns
                => fsns.SemicolonToken.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            EnumDeclarationSyntax e when e.OpenBraceToken != default
                => e.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            _ => declLine
        };
        if (declEndLine < declLine) declEndLine = declLine;

        return new FileStructureItem
        {
            SymbolString       = symbolString,
            Kind               = kind,
            StartLine          = SyntaxNodeFormatter.LeadingTriviaStartLine(node),
            EndLine            = nodeEnd,
            DeclarationLine    = declLine,
            DeclarationEndLine = declEndLine
        };
    }

    // ── usings block ───────────────────────────────────────────────────────────

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

    // ── tree walk ──────────────────────────────────────────────────────────────

    static void WalkNodes(IEnumerable<SyntaxNode> nodes, StringBuilder sb, int indent)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case FileScopedNamespaceDeclarationSyntax fsns:
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(fsns)}*/ {SyntaxNodeFormatter.Sig(fsns)}");
                    WalkNodes(fsns.Members, sb, indent + 1);
                    break;

                case NamespaceDeclarationSyntax ns:
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(ns)}*/ {SyntaxNodeFormatter.Sig(ns)}");
                    WalkNodes(ns.Members, sb, indent + 1);
                    break;

                case TypeDeclarationSyntax type:
                    foreach (var a in type.AttributeLists)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(a)}*/ {a.ToString().Trim()}");
                    XmlDocExtractor.AppendXmlDoc(type, sb, indent);
                    sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(type)}*/ {SyntaxNodeFormatter.Sig(type)}");
                    WalkNodes(type.Members, sb, indent + 1);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    foreach (var a in enumDecl.AttributeLists)
                        sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}/*{SyntaxNodeFormatter.Pos(a)}*/ {a.ToString().Trim()}");
                    XmlDocExtractor.AppendXmlDoc(enumDecl, sb, indent);
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

    static void AppendMember(MemberDeclarationSyntax member, StringBuilder sb, int indent)
    {
        var sig = MemberSignatureExtractor.GetSignature(member);
        if (string.IsNullOrWhiteSpace(sig)) return;

        sig = MemberSignatureExtractor.StripLeadingAttributes(member, sig);
        if (string.IsNullOrWhiteSpace(sig)) return;

        // 1. xmldoc comment (if any)
        XmlDocExtractor.AppendXmlDoc(member, sb, indent);

        // 2. attributes
        foreach (var attrList in member.AttributeLists)
            sb.AppendLine($"{SyntaxNodeFormatter.Pad(indent)}{attrList.ToString().Trim()}");

        // 3. signature — may be multi-line (e.g. auto-property with object initializer)
        var sigLines = sig.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var pad      = SyntaxNodeFormatter.Pad(indent);
        sb.Append($"{pad}/*{SyntaxNodeFormatter.PosWithLeadingTrivia(member)}*/ {sigLines[0]}");
        for (int i = 1; i < sigLines.Length; i++)
        {
            sb.AppendLine();
            sb.Append($"{pad}{sigLines[i]}");
        }
        sb.AppendLine();
    }
}
