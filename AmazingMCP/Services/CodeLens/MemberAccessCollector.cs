using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.CodeLens;

internal static class MemberAccessCollector
{
    internal static void CollectMemberAccess(
        MemberAccessExpressionSyntax node,
        SemanticModel model,
        INamedTypeSymbol? containingType,
        HashSet<FieldKey> seenFields,
        HashSet<PropertyKey> seenProperties,
        List<CodeLensEntry> fields,
        List<CodeLensEntry> properties)
    {
        // Skip if this is the receiver of an invocation — handled by InvocationCollector
        if (node.Parent is InvocationExpressionSyntax inv && inv.Expression == node) return;

        var symbol = model.GetSymbolInfo(node).Symbol;

        // For member access (anyExpr.Member), only collect if the receiver is 'this' (implicit or explicit).
        if (!IsThisAccess(node.Expression)) return;

        AddEntry(symbol, node.SpanStart, model, containingType, seenFields, seenProperties, fields, properties);
    }

    internal static void CollectIdentifier(
        IdentifierNameSyntax node,
        SemanticModel model,
        INamedTypeSymbol? containingType,
        HashSet<FieldKey> seenFields,
        HashSet<PropertyKey> seenProperties,
        List<CodeLensEntry> fields,
        List<CodeLensEntry> properties)
    {
        // Skip if this identifier is the member name (right side of dot) — handled by CollectMemberAccess
        if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node) return;
        // Skip if this is the method name in an invocation
        if (node.Parent is InvocationExpressionSyntax inv && inv.Expression == node) return;

        var symbol = model.GetSymbolInfo(node).Symbol;
        AddEntry(symbol, node.SpanStart, model, containingType, seenFields, seenProperties, fields, properties);
    }

    static void AddEntry(
        ISymbol? symbol,
        int spanStart,
        SemanticModel model,
        INamedTypeSymbol? containingType,
        HashSet<FieldKey> seenFields,
        HashSet<PropertyKey> seenProperties,
        List<CodeLensEntry> fields,
        List<CodeLensEntry> properties)
    {
        if (symbol is IFieldSymbol fieldSymbol)
        {
            if (containingType != null &&
                !SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, containingType))
                return;

            if (CodeLensTypeChecker.IsTrivial(fieldSymbol.Type)) return;
            var typeName = CodeLensTypeFormatter.GetDisplayName(fieldSymbol.Type);
            var key = new FieldKey(fieldSymbol.Name, typeName);
            if (!seenFields.Add(key)) return;
            fields.Add(new CodeLensEntry
            {
                Kind = CodeLensEntryKind.Field,
                VariableName = fieldSymbol.Name,
                ResolvedType = typeName,
                SourceLine = CollectorHelpers.GetSourceLine(model, spanStart),
            });
        }
        else if (symbol is IPropertySymbol propertySymbol)
        {
            if (containingType != null &&
                !SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, containingType))
                return;

            if (CodeLensTypeChecker.IsTrivial(propertySymbol.Type)) return;
            var typeName = CodeLensTypeFormatter.GetDisplayName(propertySymbol.Type);
            var key = new PropertyKey(propertySymbol.Name, typeName);
            if (!seenProperties.Add(key)) return;
            properties.Add(new CodeLensEntry
            {
                Kind = CodeLensEntryKind.Property,
                VariableName = propertySymbol.Name,
                ResolvedType = typeName,
                SourceLine = CollectorHelpers.GetSourceLine(model, spanStart),
            });
        }
    }

    /// <summary>
    /// Returns true if the expression is an explicit or implicit 'this' reference.
    /// Explicit: <c>this.Field</c> — ThisExpressionSyntax.
    /// Implicit: bare <c>Field</c> — handled by CollectIdentifier, not here.
    /// </summary>
    static bool IsThisAccess(ExpressionSyntax expression)
        => expression is ThisExpressionSyntax;
}
