using AmazingMCP.Models;
using AmazingMCP.Services.CodeLens;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AmazingMCP.Services;

/// <summary>
/// Orchestrates code lens analysis: loads the solution, finds the document,
/// walks the syntax tree in the requested line range, and formats the result.
/// </summary>
public sealed class CodeLensService(IWorkspaceProvider workspaceProvider) : ICodeLensService
{
    public async Task<string> AnalyzeAsync(
        string solutionPath,
        string filePath,
        int startLine,
        int endLine,
        CancellationToken ct = default)
    {
        var cachedSolution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);
        await cachedSolution.EnsureUpToDateAsync();

        var absolutePath = Path.GetFullPath(filePath);

        var document = cachedSolution.Solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, absolutePath, StringComparison.OrdinalIgnoreCase));

        if (document == null)
            return $"File not found in solution: {absolutePath}";

        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel == null)
            return "Could not obtain semantic model for the file.";

        var root = await document.GetSyntaxRootAsync(ct);
        if (root == null)
            return "Could not obtain syntax root for the file.";

        var text = await document.GetTextAsync(ct);

        // Clamp lines to valid range (1-based)
        if (startLine < 1) startLine = 1;
        if (endLine < startLine) endLine = startLine;
        if (endLine > text.Lines.Count) endLine = text.Lines.Count;

        var spanStart = text.Lines[startLine - 1].Start;
        var spanEnd = text.Lines[endLine - 1].End;
        var span = TextSpan.FromBounds(spanStart, spanEnd);

        // Resolve the nearest enclosing type for member-access filtering
        INamedTypeSymbol? containingType = null;
        var spanNode = root.FindNode(span);
        var nearestTypeDecl = spanNode.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (nearestTypeDecl != null)
            containingType = semanticModel.GetDeclaredSymbol(nearestTypeDecl) as INamedTypeSymbol;

        // Deduplication sets
        var seenVariables = new HashSet<VariableKey>();
        var seenCalls = new HashSet<CallKey>();
        var seenExtensions = new HashSet<ExtensionKey>();
        var seenConstructors = new HashSet<ConstructorKey>();
        var seenFields = new HashSet<FieldKey>();
        var seenProperties = new HashSet<PropertyKey>();
        var seenDefinitions = new HashSet<DefinitionKey>();

        // Output buckets
        var variables = new List<CodeLensEntry>();
        var calls = new List<CodeLensEntry>();
        var extensions = new List<CodeLensEntry>();
        var constructors = new List<CodeLensEntry>();
        var fields = new List<CodeLensEntry>();
        var properties = new List<CodeLensEntry>();
        var definitionMethods = new List<CodeLensEntry>();
        var definitionTypes = new List<CodeLensEntry>();

        foreach (var node in root.DescendantNodes(span))
        {
            if (!span.OverlapsWith(node.Span)) continue;

            CodeLensCollector.Collect(
                node, semanticModel, span, containingType,
                seenVariables, seenCalls, seenExtensions, seenConstructors, seenFields, seenProperties, seenDefinitions,
                variables, calls, extensions, constructors, fields, properties, definitionMethods, definitionTypes);
        }

        // Collect all enclosing types for the span (may be multiple for nested classes)
        var containingTypes = new List<CodeLensEntry>();
        foreach (var typeDecl in spanNode.AncestorsAndSelf().OfType<TypeDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol typeSymbol)
                DefinitionCollector.CollectContainingType(
                    typeSymbol, semanticModel, typeDecl.Identifier.SpanStart,
                    seenDefinitions, containingTypes);
        }

        return CodeLensFormatter.Format(
            variables, calls, extensions, constructors, fields, properties,
            definitionMethods, definitionTypes, containingTypes,
            sourceSnippet: ExtractSourceSnippet(text, startLine, endLine));
    }

    static string ExtractSourceSnippet(SourceText text, int startLine, int endLine)
    {
        var lines = Enumerable.Range(startLine - 1, endLine - startLine + 1)
            .Select(i => text.Lines[i].ToString());
        return string.Join("\n", lines);
    }
}
