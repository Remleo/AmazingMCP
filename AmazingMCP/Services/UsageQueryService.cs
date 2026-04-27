using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

/// <summary>
/// Traverses the Roslyn syntax tree of every document in the solution
/// and collects <see cref="UsageMatch"/> entries that satisfy the compiled predicate.
/// A single linear pass is performed per document; scope is maintained via a stack.
/// </summary>
public sealed class UsageQueryService(
    IWorkspaceProvider workspaceProvider,
    IWildcardPatternFactory wildcardFactory) : IUsageQueryService
{
    const int MatchLimit = 200;

    public async Task<(IReadOnlyList<UsageMatch> Matches, string? Error, bool Truncated)> QueryAsync(
        string solutionPath,
        string typePattern,
        string? predicate,
        IReadOnlyList<string>? scanFilters,
        CancellationToken ct = default)
    {
        var typeFilter = wildcardFactory.CreateForTypeNames(NormalizeTypePattern(typePattern));

        Func<QueryEntry, bool>? compiledPredicate = null;
        if (predicate is not null)
        {
            try
            {
                compiledPredicate = await UsagePredicateCompiler.CompileAsync(predicate);
            }
            catch (InvalidOperationException ex)
            {
                return ([], ex.Message, false);
            }
        }

        var scopePatterns = BuildScopePatterns(scanFilters);
        var cachedSolution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);
        var matches = new List<UsageMatch>();
        var truncated = false;

        foreach (var (_, compilation) in cachedSolution.Compilations)
        {
            if (truncated) break;
            ct.ThrowIfCancellationRequested();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                if (truncated) break;
                ct.ThrowIfCancellationRequested();

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(ct);
                var filePath = syntaxTree.FilePath;

                var walker = new UsageSyntaxWalker(
                    semanticModel,
                    filePath,
                    typeFilter,
                    compiledPredicate,
                    scopePatterns,
                    matches,
                    MatchLimit);

                walker.Visit(root);

                if (matches.Count >= MatchLimit)
                    truncated = true;
            }
        }

        return (matches, null, truncated);
    }

    static string NormalizeTypePattern(string pattern) =>
        !pattern.Contains('*') && !pattern.Contains('.')
            ? $"*{pattern}*"
            : pattern;

    List<IWildcardPattern>? BuildScopePatterns(IReadOnlyList<string>? scanFilters)
    {
        if (scanFilters is null || scanFilters.Count == 0) return null;
        return scanFilters
            .Select(p => wildcardFactory.CreateForTypeNames(p))
            .ToList();
    }

    // ── Inner walker ──────────────────────────────────────────────────────────

    sealed class UsageSyntaxWalker(
        SemanticModel model,
        string filePath,
        IWildcardPattern typeFilter,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? scopePatterns,
        List<UsageMatch> results,
        int limit) : CSharpSyntaxWalker(SyntaxWalkerDepth.Node)
    {
        const int LargeBlockThreshold = 5;

        // Scope stack — each frame pushed on entry, popped on exit
        readonly Stack<ScopeFrame> _scopeStack = new();

        // Current scope values (updated from stack top)
        string? _currentTypeName;
        string? _currentMethodName;
        LineRange? _currentMethodDefinitionRange;
        string _currentFilePath = filePath;

        // Depth counter for large blocks (>5 lines) — when > 0, section is suppressed
        int _largeBlockDepth;

        // ── Type declarations ─────────────────────────────────────────────────

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!TryEnterType(node, model)) return;
            base.VisitClassDeclaration(node);
            ExitType();
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (!TryEnterType(node, model)) return;
            base.VisitStructDeclaration(node);
            ExitType();
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            if (!TryEnterType(node, model)) return;
            base.VisitRecordDeclaration(node);
            ExitType();
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (!TryEnterType(node, model)) return;
            base.VisitInterfaceDeclaration(node);
            ExitType();
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            if (!TryEnterType(node, model)) return;
            base.VisitEnumDeclaration(node);
            ExitType();
        }

        bool TryEnterType(TypeDeclarationSyntax node, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol is null) return false;

            var fullName = symbol.ToDisplayString();

            // Apply scope filter — skip entire subtree if no pattern matches
            if (scopePatterns is not null && !scopePatterns.Any(p => p.IsMatch(fullName)))
                return false;

            _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange));
            _currentTypeName = fullName;
            _currentMethodName = null;
            _currentMethodDefinitionRange = null;

            // Visit primary constructor parameters for TypeAsParameter entries.
            // We do NOT set MethodDefinitionRange here — the class declaration itself
            // is the definition context and will appear as a section if matched.
            if (node.ParameterList is not null)
            {
                _currentMethodName = ".ctor";
                _currentMethodDefinitionRange = null;
                Visit(node.ParameterList);
                _currentMethodName = null;
                _currentMethodDefinitionRange = null;
            }

            return true;
        }

        bool TryEnterType(EnumDeclarationSyntax node, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol is null) return false;

            var fullName = symbol.ToDisplayString();
            if (scopePatterns is not null && !scopePatterns.Any(p => p.IsMatch(fullName)))
                return false;

            _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange));
            _currentTypeName = fullName;
            _currentMethodName = null;
            _currentMethodDefinitionRange = null;
            return true;
        }

        void ExitType()
        {
            var frame = _scopeStack.Pop();
            _currentTypeName = frame.TypeName;
            _currentMethodName = frame.MethodName;
            _currentMethodDefinitionRange = frame.MethodDefinitionRange;
        }

        // ── Method / constructor / property declarations ───────────────────────

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            EnterMethod(node.Identifier.Text, MethodDefinitionRangeResolver.Resolve(node));

            Visit(node.ReturnType);
            if (node.TypeParameterList is not null)
                Visit(node.TypeParameterList);
            foreach (var constraint in node.ConstraintClauses)
                Visit(constraint);
            Visit(node.ParameterList);
            if (node.Body is not null)
                Visit(node.Body);
            if (node.ExpressionBody is not null)
                Visit(node.ExpressionBody);

            ExitMethod();
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            EnterMethod(".ctor", MethodDefinitionRangeResolver.Resolve(node));
            Visit(node.ParameterList);
            if (node.Body is not null)
                Visit(node.Body);
            if (node.ExpressionBody is not null)
                Visit(node.ExpressionBody);
            ExitMethod();
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            EnterMethod(node.Identifier.Text, MethodDefinitionRangeResolver.Resolve(node));
            Visit(node.Type);
            if (node.AccessorList is not null)
                Visit(node.AccessorList);
            if (node.ExpressionBody is not null)
                Visit(node.ExpressionBody);
            if (node.Initializer is not null)
                Visit(node.Initializer);
            ExitMethod();
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            EnterMethod("operator " + node.OperatorToken.Text, MethodDefinitionRangeResolver.Resolve(node));
            Visit(node.ReturnType);
            if (node.Body is not null) Visit(node.Body);
            if (node.ExpressionBody is not null) Visit(node.ExpressionBody);
            ExitMethod();
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            EnterMethod("operator " + node.Type, MethodDefinitionRangeResolver.Resolve(node));
            if (node.Body is not null) Visit(node.Body);
            if (node.ExpressionBody is not null) Visit(node.ExpressionBody);
            ExitMethod();
        }

        void EnterMethod(string name, LineRange definitionRange)
        {
            _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange));
            _currentMethodName = name;
            _currentMethodDefinitionRange = definitionRange;
        }

        void ExitMethod()
        {
            var frame = _scopeStack.Pop();
            _currentMethodName = frame.MethodName;
            _currentMethodDefinitionRange = frame.MethodDefinitionRange;
        }

        // ── Large block suppression ───────────────────────────────────────────

        public override void VisitBlock(BlockSyntax node)
        {
            var span = node.GetLocation().GetLineSpan();
            var lineCount = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            var isLarge = lineCount > LargeBlockThreshold;

            if (isLarge) _largeBlockDepth++;
            base.VisitBlock(node);
            if (isLarge) _largeBlockDepth--;
        }

        // ── Usage nodes ───────────────────────────────────────────────────────

        public override void DefaultVisit(SyntaxNode node)
        {
            // Stop traversal once the limit is reached
            if (results.Count >= limit) return;

            // Only attempt to create entries when we're inside a type
            if (_currentTypeName is not null)
            {
                var entry = QueryEntryFactory.TryCreate(node, model);
                if (entry is not null
                    && typeFilter.IsMatch(entry.TypeName)
                    && (predicate is null || predicate(entry)))
                {
                    // When inside a large block, suppress section resolution —
                    // use a single-line fallback so the large block doesn't become the section
                    var section = _largeBlockDepth > 0
                        ? SectionResolver.ResolveFallback(node)
                        : SectionResolver.Resolve(node);
                    var lineSpan = node.GetLocation().GetLineSpan();
                    var matchLine = lineSpan.StartLinePosition.Line + 1;

                    var scope = new UsageScope(
                        _currentTypeName,
                        _currentFilePath,
                        _currentMethodName,
                        _currentMethodDefinitionRange,
                        section,
                        matchLine);

                    results.Add(new UsageMatch(entry, scope));
                }
            }

            base.DefaultVisit(node);
        }
    }

    // ── Scope frame ───────────────────────────────────────────────────────────

    readonly record struct ScopeFrame(string? TypeName, string? MethodName, LineRange? MethodDefinitionRange);
}
