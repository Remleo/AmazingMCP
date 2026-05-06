using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public class FileStructureService(IFileReader fileReader) : IFileStructureService
{
    public List<FileStructureItem> GetItems(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        if (!fileReader.Exists(filePath)) return [];

        var source = fileReader.ReadAllText(filePath);
        var root   = CSharpSyntaxTree.ParseText(source, path: filePath).GetRoot();
        var items  = new List<FileStructureItem>();
        CollectUsingsItem(root, items);
        CollectNodes(root.ChildNodes(), items);
        return items;
    }

    static void CollectUsingsItem(SyntaxNode root, List<FileStructureItem> items)
    {
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        if (usings.Count == 0) return;

        var startLine = usings[0].GetLocation().GetLineSpan().StartLinePosition.Line;
        var endLine   = usings[^1].GetLocation().GetLineSpan().EndLinePosition.Line;

        items.Add(new FileStructureItem
        {
            SymbolString       = "usings",
            Kind               = FileStructureItemKind.Usings,
            StartLine          = startLine,
            EndLine            = endLine,
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
        var nodeSpan = node.GetLocation().GetLineSpan();
        var nodeEnd  = nodeSpan.EndLinePosition.Line;
        var declLine = nodeSpan.StartLinePosition.Line;

        var declEndLine = node switch
        {
            TypeDeclarationSyntax t when t.OpenBraceToken != default
                => t.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            NamespaceDeclarationSyntax ns when ns.OpenBraceToken != default
                => ns.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            FileScopedNamespaceDeclarationSyntax fsns
                => fsns.SemicolonToken.GetLocation().GetLineSpan().StartLinePosition.Line,
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
            DeclarationEndLine = declEndLine
        };
    }
}
