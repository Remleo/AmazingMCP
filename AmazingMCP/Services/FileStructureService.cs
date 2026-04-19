using System.Text;
using System.Text.RegularExpressions;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public partial class FileStructureService
{
    // ── public API ─────────────────────────────────────────────────────────────

    public List<FileStructureItem> GetItems(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        if (!File.Exists(filePath)) return [];

        var source = File.ReadAllText(filePath);
        var tree   = CSharpSyntaxTree.ParseText(source, path: filePath);
        var root   = tree.GetRoot();

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

        var source = File.ReadAllText(filePath);
        var tree   = CSharpSyntaxTree.ParseText(source, path: filePath);
        var root   = tree.GetRoot();

        var sb = new StringBuilder();
        AppendUsings(root, sb);
        WalkNodes(root.ChildNodes(), sb, indent: 0);
        return sb.ToString().TrimEnd();
    }

    // ── item collection ────────────────────────────────────────────────────────

    static void CollectUsingsItem(SyntaxNode root, List<FileStructureItem> items)
    {
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .ToList();

        if (usings.Count == 0) return;

        var first     = usings[0].GetLocation().GetLineSpan();
        var last      = usings[^1].GetLocation().GetLineSpan();
        var startLine = first.StartLinePosition.Line + 1;
        var endLine   = last.EndLinePosition.Line + 1;

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
                    items.Add(MakeItem(Sig(fsns), FileStructureItemKind.Namespace, fsns));
                    CollectNodes(fsns.Members, items);
                    break;

                case NamespaceDeclarationSyntax ns:
                    items.Add(MakeItem(Sig(ns), FileStructureItemKind.Namespace, ns));
                    CollectNodes(ns.Members, items);
                    break;

                case TypeDeclarationSyntax type:
                    items.Add(MakeItem(Sig(type), FileStructureItemKind.Type, type));
                    CollectNodes(type.Members, items);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    items.Add(MakeItem(Sig(enumDecl), FileStructureItemKind.Type, enumDecl));
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
        var sig = member switch
        {
            PropertyDeclarationSyntax prop when IsAutoProperty(prop)
                => prop.ToString().Trim(),
            PropertyDeclarationSyntax prop when prop.ExpressionBody is not null
                => StripExpressionBodyProp(prop),
            PropertyDeclarationSyntax prop
                => StripPropertyBodies(prop),
            IndexerDeclarationSyntax idx when idx.ExpressionBody is not null
                => StripBodyNode(idx, idx.ExpressionBody),
            IndexerDeclarationSyntax idx when idx.AccessorList is not null
                => StripAccessorBodies(idx, idx.AccessorList),
            EventDeclarationSyntax ev
                => StripBodyNode(ev, ev.AccessorList),
            ConstructorDeclarationSyntax ctor
                => StripBody(ctor, ctor.Body, ctor.ExpressionBody),
            MethodDeclarationSyntax m
                => StripBody(m, m.Body, m.ExpressionBody),
            OperatorDeclarationSyntax op
                => StripBody(op, op.Body, op.ExpressionBody),
            ConversionOperatorDeclarationSyntax conv
                => StripBody(conv, conv.Body, conv.ExpressionBody),
            DestructorDeclarationSyntax dtor
                => StripBody(dtor, dtor.Body, dtor.ExpressionBody),
            _ => member.ToString().Trim()
        };

        if (!string.IsNullOrWhiteSpace(sig))
            items.Add(MakeItem(sig, FileStructureItemKind.Member, member));
    }

    static FileStructureItem MakeItem(string symbolString, FileStructureItemKind kind, SyntaxNode node)
    {
        var nodeSpan  = node.GetLocation().GetLineSpan();
        var nodeEnd   = nodeSpan.EndLinePosition.Line + 1;
        var declLine  = nodeSpan.StartLinePosition.Line + 1;

        // DeclarationEndLine: last line of the header before the opening brace
        var declEndLine = node switch
        {
            TypeDeclarationSyntax t when t.OpenBraceToken != default
                => t.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line, // line before {
            NamespaceDeclarationSyntax ns when ns.OpenBraceToken != default
                => ns.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            FileScopedNamespaceDeclarationSyntax fsns
                => fsns.SemicolonToken.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            EnumDeclarationSyntax e when e.OpenBraceToken != default
                => e.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line,
            _ => declLine
        };
        // ensure at least declLine
        if (declEndLine < declLine) declEndLine = declLine;

        // StartLine: include leading xmldoc/attribute trivia
        var startLine = LeadingTriviaStartLine(node);

        return new FileStructureItem
        {
            SymbolString       = symbolString,
            Kind               = kind,
            StartLine          = startLine,
            EndLine            = nodeEnd,
            DeclarationLine    = declLine,
            DeclarationEndLine = declEndLine
        };
    }

    /// Returns the first line of leading doc-comment or attribute trivia, or the node's own start line.
    static int LeadingTriviaStartLine(SyntaxNode node)
    {
        // Check for leading xmldoc trivia
        var leading = node.GetLeadingTrivia();
        foreach (var trivia in leading)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
             || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                return trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            }
        }

        // If member has attribute lists, use the first attribute's start line
        if (node is MemberDeclarationSyntax memberDecl && memberDecl.AttributeLists.Count > 0)
        {
            var firstAttr = memberDecl.AttributeLists[0];
            return firstAttr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }

        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    // ── usings block ───────────────────────────────────────────────────────────

    static void AppendUsings(SyntaxNode root, StringBuilder sb)
    {
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .ToList();

        if (usings.Count == 0) return;

        var first     = usings[0].GetLocation().GetLineSpan();
        var last      = usings[^1].GetLocation().GetLineSpan();
        var startLine = first.StartLinePosition.Line + 1;
        var endLine   = last.EndLinePosition.Line + 1;
        var lines     = endLine - startLine;

        var pos = lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";

        sb.AppendLine($"usings  {pos}");
    }

    // ── tree walk ──────────────────────────────────────────────────────────────

    static void WalkNodes(IEnumerable<SyntaxNode> nodes, StringBuilder sb, int indent)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case FileScopedNamespaceDeclarationSyntax fsns:
                    sb.AppendLine($"{Pad(indent)}{Sig(fsns)}  {Pos(fsns)}");
                    WalkNodes(fsns.Members, sb, indent + 1);
                    break;

                case NamespaceDeclarationSyntax ns:
                    sb.AppendLine($"{Pad(indent)}{Sig(ns)}  {Pos(ns)}");
                    WalkNodes(ns.Members, sb, indent + 1);
                    break;
                case TypeDeclarationSyntax type:
                    foreach (var a in type.AttributeLists)
                        sb.AppendLine($"{Pad(indent)}{a.ToString().Trim()}  {Pos(a)}");
                    AppendXmlDoc(type, sb, indent);
                    sb.AppendLine($"{Pad(indent)}{Sig(type)}  {Pos(type)}");
                    WalkNodes(type.Members, sb, indent + 1);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    foreach (var a in enumDecl.AttributeLists)
                        sb.AppendLine($"{Pad(indent)}{a.ToString().Trim()}  {Pos(a)}");
                    AppendXmlDoc(enumDecl, sb, indent);
                    sb.AppendLine($"{Pad(indent)}{Sig(enumDecl)}  {Pos(enumDecl)}");
                    foreach (var member in enumDecl.Members)
                        sb.AppendLine($"{Pad(indent + 1)}{member.ToString().Trim()}  {Pos(member)}");
                    break;

                case MemberDeclarationSyntax member:
                    AppendMember(member, sb, indent);
                    break;
            }
        }
    }

    static void AppendMember(MemberDeclarationSyntax member, StringBuilder sb, int indent)
    {
        var sig = member switch
        {
            // keep auto-properties as-is (no body to strip)
            PropertyDeclarationSyntax prop when IsAutoProperty(prop)
                => prop.ToString().Trim(),

            // expression-body property: strip the expression, keep `=> ...` replaced with `{ get; }`
            PropertyDeclarationSyntax prop when prop.ExpressionBody is not null
                => StripExpressionBodyProp(prop),

            // block-body properties: strip accessor bodies
            PropertyDeclarationSyntax prop
                => StripPropertyBodies(prop),

            // indexers
            IndexerDeclarationSyntax idx when idx.ExpressionBody is not null
                => StripBodyNode(idx, idx.ExpressionBody),
            IndexerDeclarationSyntax idx when idx.AccessorList is not null
                => StripAccessorBodies(idx, idx.AccessorList),

            // events with accessor bodies
            EventDeclarationSyntax ev
                => StripBodyNode(ev, ev.AccessorList),

            // constructors / methods / operators / destructors
            ConstructorDeclarationSyntax ctor
                => StripBody(ctor, ctor.Body, ctor.ExpressionBody),
            MethodDeclarationSyntax m
                => StripBody(m, m.Body, m.ExpressionBody),
            OperatorDeclarationSyntax op
                => StripBody(op, op.Body, op.ExpressionBody),
            ConversionOperatorDeclarationSyntax conv
                => StripBody(conv, conv.Body, conv.ExpressionBody),
            DestructorDeclarationSyntax dtor
                => StripBody(dtor, dtor.Body, dtor.ExpressionBody),

            // fields, event fields, constants — keep as-is
            _ => member.ToString().Trim()
        };

        if (string.IsNullOrWhiteSpace(sig)) return;

        // Strip leading attributes and xmldoc from sig — they are rendered separately below.
        // sig is built from member.ToString() which includes attribute lists and leading trivia.
        // We need only the part starting after the last attribute list.
        sig = StripLeadingAttributesFromSig(member, sig);

        if (string.IsNullOrWhiteSpace(sig)) return;

        // 1. xmldoc comment (if any)
        AppendXmlDoc(member, sb, indent);

        // 2. attributes (no position marker — they are part of the member block)
        foreach (var attrList in member.AttributeLists)
            sb.AppendLine($"{Pad(indent)}{attrList.ToString().Trim()}");

        // 3. member signature with position spanning the full member including xmldoc and attributes
        sb.AppendLine($"{Pad(indent)}{sig}  {PosWithLeadingTrivia(member)}");
    }

    /// Removes leading attribute lists (and any whitespace) from a sig string that was produced
    /// via member.ToString(). The attributes are rendered separately, so we only want the
    /// declaration part (return type, name, parameters, etc.).
    static string StripLeadingAttributesFromSig(MemberDeclarationSyntax member, string sig)
    {
        if (member.AttributeLists.Count == 0) return sig;

        // Find the span of the last attribute list relative to the member node start.
        var lastAttr    = member.AttributeLists[^1];
        var memberStart = member.Span.Start;
        var attrEnd     = lastAttr.Span.End;
        var relEnd      = attrEnd - memberStart;

        if (relEnd <= 0 || relEnd >= sig.Length) return sig;

        // member.ToString() starts at member.Span.Start (no leading trivia).
        return NormalizeWhitespace(sig[relEnd..]);
    }

    // ── signature extraction ───────────────────────────────────────────────────

    /// Namespace / type header: everything up to (not including) the opening brace or semicolon.
    static string Sig(SyntaxNode node)
    {
        // Use ToString() — excludes leading trivia (doc comments, attributes above)
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

    static bool IsAutoProperty(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody is not null) return false;
        if (prop.AccessorList is null) return false;
        return prop.AccessorList.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
    }

    static string StripExpressionBodyProp(PropertyDeclarationSyntax prop)
    {
        // Replace `=> expr` with `{ get; }`
        var text     = prop.ToString();
        var relStart = prop.ExpressionBody!.Span.Start - prop.Span.Start;
        if (relStart <= 0 || relStart > text.Length) return NormalizeWhitespace(text);
        return NormalizeWhitespace(text[..relStart].TrimEnd()) + " { get; }";
    }

    /// Strip block bodies from each accessor, keep the accessor keyword + modifiers.
    static string StripPropertyBodies(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList is null)
            return NormalizeWhitespace(prop.ToString().Trim());

        var text     = prop.ToString();
        var relStart = prop.AccessorList.Span.Start - prop.Span.Start;
        var result   = new StringBuilder();

        result.Append(text[..relStart].TrimEnd());
        result.Append(" { ");
        foreach (var accessor in prop.AccessorList.Accessors)
        {
            if (accessor.Modifiers.Count > 0) result.Append(accessor.Modifiers + " ");
            result.Append(accessor.Keyword);
            result.Append("; ");
        }
        result.Append('}');
        return NormalizeWhitespace(result.ToString());
    }

    static string StripAccessorBodies(IndexerDeclarationSyntax idx, AccessorListSyntax accessorList)
    {
        var text     = idx.ToString();
        var relStart = accessorList.Span.Start - idx.Span.Start;
        var result   = new StringBuilder();

        result.Append(text[..relStart].TrimEnd());
        result.Append(" { ");
        foreach (var accessor in accessorList.Accessors)
        {
            if (accessor.Modifiers.Count > 0) result.Append(accessor.Modifiers + " ");
            result.Append(accessor.Keyword);
            result.Append("; ");
        }
        result.Append('}');
        return NormalizeWhitespace(result.ToString());
    }

    /// Replace body/expressionBody span with `;`.
    static string StripBody(SyntaxNode node, BlockSyntax? body, ArrowExpressionClauseSyntax? exprBody)
    {
        SyntaxNode? toRemove = (SyntaxNode?)body ?? exprBody;
        if (toRemove is null)
            return NormalizeWhitespace(node.ToString().Trim());

        var text     = node.ToString();
        var relStart = toRemove.Span.Start - node.Span.Start;

        if (relStart <= 0 || relStart > text.Length)
            return NormalizeWhitespace(text.Trim());

        return NormalizeWhitespace(text[..relStart].TrimEnd().TrimEnd(';').TrimEnd()) + ";";
    }

    /// Replace a child node span entirely (e.g. event accessor list).
    static string StripBodyNode(SyntaxNode parent, SyntaxNode? child)
    {
        if (child is null) return NormalizeWhitespace(parent.ToString().Trim());
        var text     = parent.ToString();
        var relStart = child.Span.Start - parent.Span.Start;
        if (relStart <= 0 || relStart > text.Length)
            return NormalizeWhitespace(text.Trim());
        return NormalizeWhitespace(text[..relStart].TrimEnd().TrimEnd(';').TrimEnd()) + ";";
    }

    // ── position ───────────────────────────────────────────────────────────────

    static string Pos(SyntaxNode node)
    {
        var span      = node.GetLocation().GetLineSpan();
        var startLine = span.StartLinePosition.Line + 1;
        var endLine   = span.EndLinePosition.Line + 1;
        var lines     = endLine - startLine;

        return lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";
    }

    /// Like Pos, but startLine includes leading xmldoc/attribute trivia.
    static string PosWithLeadingTrivia(SyntaxNode node)
    {
        var span      = node.GetLocation().GetLineSpan();
        var endLine   = span.EndLinePosition.Line + 1;
        var startLine = LeadingTriviaStartLine(node);
        var lines     = endLine - startLine;

        return lines > 0 ? $"[lines:{startLine} +{lines}]" : $"[line:{startLine}]";
    }

    // ── xml doc summary ────────────────────────────────────────────────────────

    static void AppendXmlDoc(SyntaxNode node, StringBuilder sb, int indent)
    {
        var summary = ExtractSummary(node);
        if (summary is null) return;
        sb.AppendLine($"{Pad(indent)}/// {summary}");
    }

    static string? ExtractSummary(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                              || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (trivia == default) return null;

        var xml = trivia.GetStructure();
        if (xml is null) return null;

        // find <summary>...</summary>
        var summaryElement = xml.DescendantNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.LocalName.Text == "summary");

        if (summaryElement is null) return null;

        // collect all text content inside <summary>
        var text = string.Concat(
            summaryElement.Content
                .Select(c => c switch
                {
                    XmlTextSyntax t => string.Concat(t.TextTokens.Select(tok => tok.ValueText)),
                    _ => ""
                }));

        // normalize whitespace
        text = WhitespaceRegex().Replace(text.Trim(), " ");

        if (string.IsNullOrWhiteSpace(text)) return null;

        return text.Length > 200 ? text[..200] + "…" : text;
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    static string Pad(int indent) => new(' ', indent * 4);

    /// Collapse runs of whitespace/newlines into a single space.
    static string NormalizeWhitespace(string s) =>
        WhitespaceRegex().Replace(s.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
