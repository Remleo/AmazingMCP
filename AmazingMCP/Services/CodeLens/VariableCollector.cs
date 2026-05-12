using AmazingMCP.Models;
using AmazingMCP.Models.CodeLens;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace AmazingMCP.Services.CodeLens;

internal static class VariableCollector
{
    /// <summary>
    /// Collects a local variable from a read/write usage site (IdentifierNameSyntax → ILocalSymbol).
    /// </summary>
    internal static void CollectIdentifierUsage(
        IdentifierNameSyntax node,
        SemanticModel model,
        HashSet<VariableKey> seen,
        List<CodeLensEntry> output)
    {
        // Skip if this is the member name on the right side of a dot
        if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node) return;
        // Skip if this is the method name in an invocation
        if (node.Parent is InvocationExpressionSyntax inv && inv.Expression == node) return;

        if (model.GetSymbolInfo(node).Symbol is not ILocalSymbol symbol) return;
        TryAdd(symbol.Name, symbol.Type, node.SpanStart, model, seen, output);
    }

    static void TryAdd(
        string name,
        ITypeSymbol type,
        int spanStart,
        SemanticModel model,
        HashSet<VariableKey> seen,
        List<CodeLensEntry> output)
    {
        if (CodeLensTypeChecker.IsTrivial(type)) return;

        var typeName = CodeLensTypeFormatter.GetDisplayName(type);
        var key = new VariableKey(name, typeName);
        if (!seen.Add(key)) return;

        var sourceLine = CollectorHelpers.GetSourceLine(model, spanStart);
        output.Add(new CodeLensEntry
        {
            Kind = CodeLensEntryKind.Variable,
            VariableName = name,
            ResolvedType = typeName,
            SourceLine = sourceLine,
        });
    }
}
