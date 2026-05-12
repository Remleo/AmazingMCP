using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.FileAnalysis;

/// Extracts stripped signatures from member declaration syntax nodes,
/// removing bodies while preserving the declaration header.
internal static class MemberSignatureExtractor
{
    internal static string GetSignature(MemberDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax prop when IsAutoProperty(prop)
            => SyntaxNodeFormatter.NormalizeWhitespacePreserveIndent(prop.ToString().Trim()),

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

        _ => SyntaxNodeFormatter.NormalizeWhitespacePreserveIndent(member.ToString().Trim())
    };

    /// Removes leading attribute lists from a sig string produced via member.ToString().
    internal static string StripLeadingAttributes(MemberDeclarationSyntax member, string sig)
    {
        if (member.AttributeLists.Count == 0) return sig;

        var lastAttr    = member.AttributeLists[^1];
        var memberStart = member.Span.Start;
        var attrEnd     = lastAttr.Span.End;
        var relEnd      = attrEnd - memberStart;

        if (relEnd <= 0 || relEnd >= sig.Length) return sig;

        return SyntaxNodeFormatter.NormalizeWhitespacePreserveIndent(sig[relEnd..]);
    }

    // ── private helpers ────────────────────────────────────────────────────────

    static bool IsAutoProperty(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody is not null) return false;
        if (prop.AccessorList is null) return false;
        return prop.AccessorList.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
    }

    static string StripExpressionBodyProp(PropertyDeclarationSyntax prop)
    {
        var text     = prop.ToString();
        var relStart = prop.ExpressionBody!.Span.Start - prop.Span.Start;
        if (relStart <= 0 || relStart > text.Length)
            return SyntaxNodeFormatter.NormalizeWhitespace(text);
        return SyntaxNodeFormatter.NormalizeWhitespace(text[..relStart].TrimEnd()) + " { get; }";
    }

    static string StripPropertyBodies(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList is null)
            return SyntaxNodeFormatter.NormalizeWhitespace(prop.ToString().Trim());

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
        return SyntaxNodeFormatter.NormalizeWhitespace(result.ToString());
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
        return SyntaxNodeFormatter.NormalizeWhitespace(result.ToString());
    }

    static string StripBody(
        Microsoft.CodeAnalysis.SyntaxNode node,
        BlockSyntax? body,
        ArrowExpressionClauseSyntax? exprBody)
    {
        var toRemove = (Microsoft.CodeAnalysis.SyntaxNode?)body ?? exprBody;
        if (toRemove is null)
            return SyntaxNodeFormatter.NormalizeWhitespace(node.ToString().Trim());

        var text     = node.ToString();
        var relStart = toRemove.Span.Start - node.Span.Start;

        if (relStart <= 0 || relStart > text.Length)
            return SyntaxNodeFormatter.NormalizeWhitespace(text.Trim());

        return SyntaxNodeFormatter.NormalizeWhitespace(text[..relStart].TrimEnd().TrimEnd(';').TrimEnd()) + ";";
    }

    static string StripBodyNode(
        Microsoft.CodeAnalysis.SyntaxNode parent,
        Microsoft.CodeAnalysis.SyntaxNode? child)
    {
        if (child is null) return SyntaxNodeFormatter.NormalizeWhitespace(parent.ToString().Trim());
        var text     = parent.ToString();
        var relStart = child.Span.Start - parent.Span.Start;
        if (relStart <= 0 || relStart > text.Length)
            return SyntaxNodeFormatter.NormalizeWhitespace(text.Trim());
        return SyntaxNodeFormatter.NormalizeWhitespace(text[..relStart].TrimEnd().TrimEnd(';').TrimEnd()) + ";";
    }
}
