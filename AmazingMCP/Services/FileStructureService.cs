using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

public class FileStructureService
{
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
        WalkMembers(root.ChildNodes(), sb, indent: 0);
        return sb.ToString().TrimEnd();
    }

    // ── usings ─────────────────────────────────────────────────────────────────

    static void AppendUsings(SyntaxNode root, StringBuilder sb)
    {
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .ToList();

        if (usings.Count == 0) return;

        var first = usings[0].GetLocation().GetLineSpan();
        var last  = usings[^1].GetLocation().GetLineSpan();

        var startLine = first.StartLinePosition.Line + 1;
        var endLine   = last.EndLinePosition.Line + 1;
        var col       = first.StartLinePosition.Character + 1;
        var lines     = endLine - startLine;

        var pos = lines > 0
            ? $"[line:{startLine}, +{lines} lines, col:{col}]"
            : $"[line:{startLine}, col:{col}]";

        sb.AppendLine($"usings  {pos}");
    }

    // ── position helpers ───────────────────────────────────────────────────────

    static string Pos(SyntaxNode node)
    {
        var span      = node.GetLocation().GetLineSpan();
        var startLine = span.StartLinePosition.Line + 1;
        var endLine   = span.EndLinePosition.Line + 1;
        var col       = span.StartLinePosition.Character + 1;
        var lines     = endLine - startLine;

        return lines > 0
            ? $"[line:{startLine}, +{lines} lines, col:{col}]"
            : $"[line:{startLine}, col:{col}]";
    }

    static string PosToken(SyntaxToken token)
    {
        var span = token.GetLocation().GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var col  = span.StartLinePosition.Character + 1;
        return $"[line:{line}, col:{col}]";
    }

    static string Indent(int level) => new(' ', level * 4);

    // ── walk ───────────────────────────────────────────────────────────────────

    static void WalkMembers(IEnumerable<SyntaxNode> nodes, StringBuilder sb, int indent)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case FileScopedNamespaceDeclarationSyntax fsns:
                    sb.AppendLine($"{Indent(indent)}namespace {fsns.Name}  {Pos(fsns)}");
                    WalkMembers(fsns.Members, sb, indent + 1);
                    break;

                case NamespaceDeclarationSyntax ns:
                    sb.AppendLine($"{Indent(indent)}namespace {ns.Name}  {Pos(ns)}");
                    WalkMembers(ns.Members, sb, indent + 1);
                    break;

                case TypeDeclarationSyntax type:
                    AppendType(type, sb, indent);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    AppendEnum(enumDecl, sb, indent);
                    break;
            }
        }
    }

    static void AppendType(TypeDeclarationSyntax type, StringBuilder sb, int indent)
    {
        foreach (var attrList in type.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var sig = BuildTypeSignature(type);
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(type)}");

        foreach (var member in type.Members)
            AppendMember(member, sb, indent + 1);
    }

    static void AppendEnum(EnumDeclarationSyntax enumDecl, StringBuilder sb, int indent)
    {
        foreach (var attrList in enumDecl.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods = enumDecl.Modifiers.ToString();
        var sig  = string.IsNullOrEmpty(mods)
            ? $"enum {enumDecl.Identifier}"
            : $"{mods} enum {enumDecl.Identifier}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(enumDecl)}");

        foreach (var member in enumDecl.Members)
        {
            var val = member.EqualsValue is not null ? $" = {member.EqualsValue.Value}" : "";
            sb.AppendLine($"{Indent(indent + 1)}{member.Identifier}{val}  {PosToken(member.Identifier)}");
        }
    }

    static void AppendMember(MemberDeclarationSyntax member, StringBuilder sb, int indent)
    {
        switch (member)
        {
            case TypeDeclarationSyntax nested:
                AppendType(nested, sb, indent);
                break;

            case EnumDeclarationSyntax nestedEnum:
                AppendEnum(nestedEnum, sb, indent);
                break;

            case FieldDeclarationSyntax field:
                AppendField(field, sb, indent);
                break;

            case PropertyDeclarationSyntax prop:
                AppendProperty(prop, sb, indent);
                break;

            case ConstructorDeclarationSyntax ctor:
                AppendConstructor(ctor, sb, indent);
                break;

            case MethodDeclarationSyntax method:
                AppendMethod(method, sb, indent);
                break;

            case OperatorDeclarationSyntax op:
                AppendOperator(op, sb, indent);
                break;

            case ConversionOperatorDeclarationSyntax conv:
                AppendConversionOperator(conv, sb, indent);
                break;

            case IndexerDeclarationSyntax indexer:
                AppendIndexer(indexer, sb, indent);
                break;

            case EventDeclarationSyntax ev:
                AppendEvent(ev, sb, indent);
                break;

            case EventFieldDeclarationSyntax evField:
                AppendEventField(evField, sb, indent);
                break;

            case DestructorDeclarationSyntax dtor:
                AppendDestructor(dtor, sb, indent);
                break;
        }
    }

    // ── member formatters ──────────────────────────────────────────────────────

    static void AppendField(FieldDeclarationSyntax field, StringBuilder sb, int indent)
    {
        foreach (var attrList in field.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods     = field.Modifiers.ToString();
        var typeName = field.Declaration.Type;

        foreach (var variable in field.Declaration.Variables)
        {
            var init = variable.Initializer is not null ? $" {variable.Initializer}" : "";
            var sig  = string.IsNullOrEmpty(mods)
                ? $"{typeName} {variable.Identifier}{init}"
                : $"{mods} {typeName} {variable.Identifier}{init}";
            sb.AppendLine($"{Indent(indent)}{sig}  {PosToken(variable.Identifier)}");
        }
    }

    static void AppendProperty(PropertyDeclarationSyntax prop, StringBuilder sb, int indent)
    {
        foreach (var attrList in prop.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods      = prop.Modifiers.ToString();
        var accessors = FormatPropertyAccessors(prop);
        var sig       = string.IsNullOrEmpty(mods)
            ? $"{prop.Type} {prop.Identifier} {accessors}"
            : $"{mods} {prop.Type} {prop.Identifier} {accessors}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(prop)}");
    }

    static void AppendConstructor(ConstructorDeclarationSyntax ctor, StringBuilder sb, int indent)
    {
        foreach (var attrList in ctor.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods    = ctor.Modifiers.ToString();
        var parms   = ctor.ParameterList.ToString();
        var init    = ctor.Initializer is not null ? $" {ctor.Initializer}" : "";
        var sig     = string.IsNullOrEmpty(mods)
            ? $"{ctor.Identifier}{parms}{init}"
            : $"{mods} {ctor.Identifier}{parms}{init}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(ctor)}");
    }

    static void AppendMethod(MethodDeclarationSyntax method, StringBuilder sb, int indent)
    {
        foreach (var attrList in method.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods        = method.Modifiers.ToString();
        var typeParams  = method.TypeParameterList?.ToString() ?? "";
        var parms       = method.ParameterList.ToString();
        var constraints = method.ConstraintClauses.Count > 0
            ? " " + string.Join(" ", method.ConstraintClauses.Select(c => c.ToString()))
            : "";
        var sig = string.IsNullOrEmpty(mods)
            ? $"{method.ReturnType} {method.Identifier}{typeParams}{parms}{constraints}"
            : $"{mods} {method.ReturnType} {method.Identifier}{typeParams}{parms}{constraints}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(method)}");
    }

    static void AppendOperator(OperatorDeclarationSyntax op, StringBuilder sb, int indent)
    {
        foreach (var attrList in op.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods = op.Modifiers.ToString();
        var sig  = string.IsNullOrEmpty(mods)
            ? $"{op.ReturnType} operator {op.OperatorToken}{op.ParameterList}"
            : $"{mods} {op.ReturnType} operator {op.OperatorToken}{op.ParameterList}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(op)}");
    }

    static void AppendConversionOperator(ConversionOperatorDeclarationSyntax conv, StringBuilder sb, int indent)
    {
        foreach (var attrList in conv.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods = conv.Modifiers.ToString();
        var sig  = string.IsNullOrEmpty(mods)
            ? $"{conv.ImplicitOrExplicitKeyword} operator {conv.Type}{conv.ParameterList}"
            : $"{mods} {conv.ImplicitOrExplicitKeyword} operator {conv.Type}{conv.ParameterList}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(conv)}");
    }

    static void AppendIndexer(IndexerDeclarationSyntax indexer, StringBuilder sb, int indent)
    {
        foreach (var attrList in indexer.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods      = indexer.Modifiers.ToString();
        var accessors = FormatIndexerAccessors(indexer);
        var sig       = string.IsNullOrEmpty(mods)
            ? $"{indexer.Type} this{indexer.ParameterList} {accessors}"
            : $"{mods} {indexer.Type} this{indexer.ParameterList} {accessors}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(indexer)}");
    }

    static void AppendEvent(EventDeclarationSyntax ev, StringBuilder sb, int indent)
    {
        foreach (var attrList in ev.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods = ev.Modifiers.ToString();
        var sig  = string.IsNullOrEmpty(mods)
            ? $"event {ev.Type} {ev.Identifier}"
            : $"{mods} event {ev.Type} {ev.Identifier}";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(ev)}");
    }

    static void AppendEventField(EventFieldDeclarationSyntax evField, StringBuilder sb, int indent)
    {
        foreach (var attrList in evField.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods = evField.Modifiers.ToString();
        foreach (var variable in evField.Declaration.Variables)
        {
            var sig = string.IsNullOrEmpty(mods)
                ? $"event {evField.Declaration.Type} {variable.Identifier}"
                : $"{mods} event {evField.Declaration.Type} {variable.Identifier}";
            sb.AppendLine($"{Indent(indent)}{sig}  {PosToken(variable.Identifier)}");
        }
    }

    static void AppendDestructor(DestructorDeclarationSyntax dtor, StringBuilder sb, int indent)
    {
        foreach (var attrList in dtor.AttributeLists)
            sb.AppendLine($"{Indent(indent)}{attrList.ToFullString().Trim()}  {PosToken(attrList.OpenBracketToken)}");

        var mods = dtor.Modifiers.ToString();
        var sig  = string.IsNullOrEmpty(mods)
            ? $"~{dtor.Identifier}()"
            : $"{mods} ~{dtor.Identifier}()";
        sb.AppendLine($"{Indent(indent)}{sig}  {Pos(dtor)}");
    }

    // ── signature builders ─────────────────────────────────────────────────────

    static string BuildTypeSignature(TypeDeclarationSyntax type)
    {
        var mods        = type.Modifiers.ToString();
        var keyword     = type.Keyword.ToString();
        var name        = type.Identifier.ToString();
        var typeParams  = type.TypeParameterList?.ToString() ?? "";
        var bases       = type.BaseList is not null ? $" {type.BaseList}" : "";
        var constraints = type.ConstraintClauses.Count > 0
            ? " " + string.Join(" ", type.ConstraintClauses.Select(c => c.ToString()))
            : "";

        return string.IsNullOrEmpty(mods)
            ? $"{keyword} {name}{typeParams}{bases}{constraints}"
            : $"{mods} {keyword} {name}{typeParams}{bases}{constraints}";
    }

    static string FormatPropertyAccessors(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody is not null)
            return "{ get; }";

        if (prop.AccessorList is null)
            return "";

        var parts = prop.AccessorList.Accessors.Select(a =>
        {
            var mods    = a.Modifiers.ToString();
            var keyword = a.Keyword.ToString();
            return string.IsNullOrEmpty(mods) ? $"{keyword};" : $"{mods} {keyword};";
        });

        return "{ " + string.Join(" ", parts) + " }";
    }

    static string FormatIndexerAccessors(IndexerDeclarationSyntax indexer)
    {
        if (indexer.ExpressionBody is not null)
            return "{ get; }";

        if (indexer.AccessorList is null)
            return "";

        var parts = indexer.AccessorList.Accessors.Select(a =>
        {
            var mods    = a.Modifiers.ToString();
            var keyword = a.Keyword.ToString();
            return string.IsNullOrEmpty(mods) ? $"{keyword};" : $"{mods} {keyword};";
        });

        return "{ " + string.Join(" ", parts) + " }";
    }
}
