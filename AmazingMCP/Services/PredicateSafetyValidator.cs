using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

/// <summary>
/// Validates that a predicate expression string does not contain unsafe constructs.
/// Instance method calls on any type are always allowed.
/// Static calls are restricted to a whitelist of known safe types.
/// </summary>
public static class PredicateSafetyValidator
{
    static readonly HashSet<string> AllowedStaticReceivers = new(StringComparer.Ordinal)
    {
        // Short names
        "Enumerable", "String", "Math", "Convert", "Enum", "Type",
        // Full names
        "System.Linq.Enumerable", "System.String", "System.Math",
        "System.Convert", "System.Enum", "System.Type",
    };

    static readonly string AllowedShortNames =
        string.Join(", ", new[] { "Enumerable", "String", "Math", "Convert", "Enum", "Type" });

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the expression contains unsafe constructs.
    /// </summary>
    public static void Validate(string expression)
    {
        var tree = CSharpSyntaxTree.ParseText(
            $"_ = {expression};",
            new CSharpParseOptions(kind: SourceCodeKind.Regular));

        var root = tree.GetRoot();
        var violations = new List<string>();

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case TypeDeclarationSyntax t:
                    violations.Add($"Type declaration not allowed: {t.Identifier}");
                    break;

                case MethodDeclarationSyntax m:
                    violations.Add($"Method declaration not allowed: {m.Identifier}");
                    break;

                case LambdaExpressionSyntax:
                    violations.Add("Lambda expressions are not allowed in predicates");
                    break;

                case AnonymousMethodExpressionSyntax:
                    violations.Add("Anonymous methods are not allowed in predicates");
                    break;

                case ObjectCreationExpressionSyntax oc:
                    violations.Add($"Object creation not allowed: new {oc.Type}");
                    break;

                case ImplicitObjectCreationExpressionSyntax:
                    violations.Add("Object creation not allowed");
                    break;

                case InvocationExpressionSyntax inv when IsStaticStyleInvocation(inv, out var receiver):
                    if (!AllowedStaticReceivers.Contains(receiver))
                        violations.Add(
                            $"Static call not allowed on '{receiver}'. Allowed: {AllowedShortNames}");
                    break;
            }
        }

        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"Unsafe predicate expression:\n{string.Join("\n", violations.Select(v => $"  - {v}"))}");
    }

    // ── Static call detection ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the invocation looks like a static call — receiver is a plain type name
    /// or a qualified name, not a variable/property access chain starting with a lowercase identifier.
    /// </summary>
    static bool IsStaticStyleInvocation(InvocationExpressionSyntax inv, out string receiver)
    {
        receiver = string.Empty;

        if (inv.Expression is not MemberAccessExpressionSyntax ma)
            return false;

        var receiverNode = ma.Expression;

        // Simple identifier starting with uppercase → likely a type name (e.g. Math, String)
        if (receiverNode is IdentifierNameSyntax id)
        {
            if (id.Identifier.Text.Length == 0 || !char.IsUpper(id.Identifier.Text[0]))
                return false;
            receiver = id.Identifier.Text;
            return true;
        }

        // Qualified name like System.Math or System.Linq.Enumerable
        if (receiverNode is QualifiedNameSyntax qn)
        {
            receiver = qn.ToString();
            return true;
        }

        // Chained member access: System.Math.Abs(...) vs x.Prop.Method(...)
        // Distinguish by the leftmost identifier — uppercase = type, lowercase = variable
        if (receiverNode is MemberAccessExpressionSyntax chainedMa)
        {
            var leftmost = GetLeftmostIdentifier(chainedMa);
            if (leftmost is null || !char.IsUpper(leftmost[0])) return false;

            receiver = receiverNode.ToString();
            return true;
        }

        return false;
    }

    static string? GetLeftmostIdentifier(MemberAccessExpressionSyntax ma)
    {
        SyntaxNode current = ma;
        while (current is MemberAccessExpressionSyntax inner)
            current = inner.Expression;

        return current is IdentifierNameSyntax id ? id.Identifier.Text : null;
    }
}
