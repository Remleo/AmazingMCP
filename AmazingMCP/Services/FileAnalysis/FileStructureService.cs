using AmazingMCP.Models;
using AmazingMCP.Models.FileAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.FileAnalysis;

public class FileStructureService : IFileStructureService
{
    public List<FileStructureItem> GetItems(string source)
    {
        var root  = CSharpSyntaxTree.ParseText(source).GetRoot();
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
            Name               = FileStructureItem.UsingsAlias,
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
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(fsns), fsns.Name.ToString(), FileStructureItemKind.Namespace, fsns));
                    CollectNodes(fsns.Members, items);
                    break;

                case NamespaceDeclarationSyntax ns:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(ns), ns.Name.ToString(), FileStructureItemKind.Namespace, ns));
                    CollectNodes(ns.Members, items);
                    break;

                case TypeDeclarationSyntax type:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(type), type.Identifier.Text, FileStructureItemKind.Type, type));
                    CollectNodes(type.Members, items);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    items.Add(MakeItem(SyntaxNodeFormatter.Sig(enumDecl), enumDecl.Identifier.Text, FileStructureItemKind.Type, enumDecl));
                    foreach (var member in enumDecl.Members)
                        items.Add(MakeItem(member.ToString().Trim(), member.Identifier.Text, FileStructureItemKind.Member, member));
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
        if (string.IsNullOrWhiteSpace(sig)) return;

        var name = GetMemberName(member);

        var aliases = member is ConstructorDeclarationSyntax
            ? [FileStructureItem.ConstructorAlias]
            : (string[]?)null;

        items.Add(MakeItem(sig, name, FileStructureItemKind.Member, member, aliases));
    }

    static string GetMemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        PropertyDeclarationSyntax p => p.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.Text,
        EventDeclarationSyntax e => e.Identifier.Text,
        IndexerDeclarationSyntax => "this",
        OperatorDeclarationSyntax op => op.OperatorToken.Text,
        ConversionOperatorDeclarationSyntax conv => conv.Type.ToString(),
        DestructorDeclarationSyntax d => $"~{d.Identifier.Text}",
        _ => ""
    };

    static FileStructureItem MakeItem(string symbolString, string name, FileStructureItemKind kind, SyntaxNode node, string[]? aliases = null)
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
            Name               = name,
            Kind               = kind,
            StartLine          = SyntaxNodeFormatter.LeadingTriviaStartLine(node),
            EndLine            = nodeEnd,
            DeclarationEndLine = declEndLine,
            NameAliases = aliases
        };
    }
}
