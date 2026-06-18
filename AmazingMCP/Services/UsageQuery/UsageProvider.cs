using AmazingMCP.Configuration;
using AmazingMCP.Models;
using AmazingMCP.Models.FileAnalysis;
using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Options;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Traverses the Roslyn syntax tree of every document in the solution
/// and collects <see cref="UsageMatch"/> entries that satisfy the compiled predicate.
/// A single linear pass is performed per document; scope is maintained via a stack.
/// </summary>
public sealed class UsageProvider(
    IWorkspaceProvider workspaceProvider,
    IWildcardPatternFactory wildcardFactory,
    IInheritanceUsageProvider inheritanceUsageProvider,
    IOptions<QueryUsagesOptions> options) : IUsageProvider
{
    readonly QueryUsagesOptions _options = options.Value;

    public async Task<(IReadOnlyList<UsageMatch> Matches, string? Error, bool Truncated)> QueryAsync(
        string solutionPath,
        string typeName,
        string? predicate,
        IReadOnlyList<string>? scanInclude,
        IReadOnlyList<string>? scanExclude,
        CancellationToken ct = default)
    {
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

        var includePatterns = BuildScopePatterns(scanInclude);
        var excludePatterns = BuildScopePatterns(scanExclude);
        var cachedSolution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);
        var matches = new List<UsageMatch>();
        var truncated = false;
        var interfaceMemberCache = new InterfaceMemberCache();

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
                    typeName,
                    compiledPredicate,
                    includePatterns,
                    excludePatterns,
                    matches,
                    _options.QueryMatchLimit,
                    interfaceMemberCache);

                walker.Visit(root);

                if (matches.Count >= _options.QueryMatchLimit)
                    truncated = true;
            }
        }

        var inheritanceMatches = FindInheritanceMatches(cachedSolution, typeName, compiledPredicate, includePatterns, excludePatterns);
        matches.AddRange(inheritanceMatches);

        return (matches, null, truncated);
    }

    IReadOnlyList<UsageMatch> FindInheritanceMatches(
        ICachedSolution cachedSolution,
        string typeName,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? includePatterns,
        List<IWildcardPattern>? excludePatterns) =>
        inheritanceUsageProvider.FindMatches(cachedSolution, typeName, predicate, includePatterns, excludePatterns);

    List<IWildcardPattern>? BuildScopePatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0) return null;
        return patterns
            .Select(wildcardFactory.CreateGlob)
            .ToList();
    }

    // ── Inner walker ──────────────────────────────────────────────────────────

    sealed class UsageSyntaxWalker(
        SemanticModel model,
        string filePath,
        string typeName,
        Func<QueryEntry, bool>? predicate,
        List<IWildcardPattern>? includePatterns,
        List<IWildcardPattern>? excludePatterns,
        List<UsageMatch> results,
        int limit,
        IInterfaceMemberCache interfaceMemberCache) : CSharpSyntaxWalker(SyntaxWalkerDepth.Node)
    {
        // Scope stack — each frame pushed on entry, popped on exit
        readonly Stack<ScopeFrame> _scopeStack = new();

        // Current scope values (updated from stack top)
        string? _currentTypeName;
        string? _currentMethodName;
        LineRange? _currentMethodDefinitionRange;
        LineRange? _currentMethodFullRange;
        string _currentFilePath = filePath;

        // ── Type declarations ─────────────────────────────────────────────────

        // Top-level statements are hosted inside a compiler-synthesized type with a <Main>$ method.
        // Establish that scope so usages inside the statements are captured by DefaultVisit.
        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var firstStatement = node.Members.OfType<GlobalStatementSyntax>().FirstOrDefault();
            var method = firstStatement is not null ? model.GetEnclosingSymbol(firstStatement.SpanStart) as IMethodSymbol : null;
            var typeFullName = method?.ContainingType?.ToDisplayString();

            var entered = typeFullName is not null
                && (includePatterns is null || includePatterns.Any(p => p.IsMatch(typeFullName)))
                && (excludePatterns is null || !excludePatterns.Any(p => p.IsMatch(typeFullName)));

            if (entered)
            {
                _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange, _currentMethodFullRange));
                _currentTypeName = typeFullName;
                _currentMethodName = method!.Name;
            }

            base.VisitCompilationUnit(node);
            if (entered) ExitType();
        }

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

            // Apply scope filters — skip entire subtree if include patterns don't match
            // or if exclude patterns match
            if (includePatterns is not null && !includePatterns.Any(p => p.IsMatch(fullName)))
                return false;
            if (excludePatterns is not null && excludePatterns.Any(p => p.IsMatch(fullName)))
                return false;

            _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange, _currentMethodFullRange));
            _currentTypeName = fullName;
            _currentMethodName = null;
            _currentMethodDefinitionRange = null;
            _currentMethodFullRange = null;

            // Visit primary constructor parameters for Parameter entries.
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
            if (includePatterns is not null && !includePatterns.Any(p => p.IsMatch(fullName)))
                return false;
            if (excludePatterns is not null && excludePatterns.Any(p => p.IsMatch(fullName)))
                return false;

            _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange, _currentMethodFullRange));
            _currentTypeName = fullName;
            _currentMethodName = null;
            _currentMethodDefinitionRange = null;
            _currentMethodFullRange = null;
            return true;
        }

        void ExitType()
        {
            var frame = _scopeStack.Pop();
            _currentTypeName = frame.TypeName;
            _currentMethodName = frame.MethodName;
            _currentMethodDefinitionRange = frame.MethodDefinitionRange;
            _currentMethodFullRange = frame.MethodFullRange;
        }

        // ── Method / constructor / property declarations ───────────────────────

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            EnterMethod(node.Identifier.Text,
                DeclarationRangeResolver.Resolve(node),
                DeclarationRangeResolver.ResolveFullRange(node));

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
            EnterMethod(".ctor",
                DeclarationRangeResolver.Resolve(node),
                DeclarationRangeResolver.ResolveFullRange(node));
            Visit(node.ParameterList);
            if (node.Body is not null)
                Visit(node.Body);
            if (node.ExpressionBody is not null)
                Visit(node.ExpressionBody);
            ExitMethod();
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            EnterMethod(node.Identifier.Text,
                DeclarationRangeResolver.Resolve(node),
                DeclarationRangeResolver.ResolveFullRange(node));
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
            EnterMethod("operator " + node.OperatorToken.Text,
                DeclarationRangeResolver.Resolve(node),
                DeclarationRangeResolver.ResolveFullRange(node));
            Visit(node.ReturnType);
            if (node.Body is not null) Visit(node.Body);
            if (node.ExpressionBody is not null) Visit(node.ExpressionBody);
            ExitMethod();
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            EnterMethod("operator " + node.Type,
                DeclarationRangeResolver.Resolve(node),
                DeclarationRangeResolver.ResolveFullRange(node));
            if (node.Body is not null) Visit(node.Body);
            if (node.ExpressionBody is not null) Visit(node.ExpressionBody);
            ExitMethod();
        }

        void EnterMethod(string name, LineRange definitionRange, LineRange fullRange)
        {
            _scopeStack.Push(new ScopeFrame(_currentTypeName, _currentMethodName, _currentMethodDefinitionRange, _currentMethodFullRange));
            _currentMethodName = name;
            _currentMethodDefinitionRange = definitionRange;
            _currentMethodFullRange = fullRange;
        }

        void ExitMethod()
        {
            var frame = _scopeStack.Pop();
            _currentMethodName = frame.MethodName;
            _currentMethodDefinitionRange = frame.MethodDefinitionRange;
            _currentMethodFullRange = frame.MethodFullRange;
        }

        // ── Usage nodes ───────────────────────────────────────────────────────

        public override void DefaultVisit(SyntaxNode node)
        {
            if (results.Count >= limit) return;

            if (_currentTypeName is not null)
                ProcessNode(node);

            base.DefaultVisit(node);
        }

        void ProcessNode(SyntaxNode node)
        {
            foreach (var entry in QueryEntryFactory.TryCreate(node, model))
            {
                TryAddMatch(entry, node);
                if (results.Count >= limit) break;

                AddInterfaceEntries(entry, node);
                if (results.Count >= limit) break;
            }
        }

        void AddInterfaceEntries(QueryEntry entry, SyntaxNode node)
        {
            var memberSymbol = TryGetMemberSymbol(node, model, entry);
            if (memberSymbol is null) return;

            foreach (var ifaceMember in interfaceMemberCache.GetInterfaceMembers(memberSymbol))
            {
                var ifaceEntry = new QueryEntry
                {
                    Kind = entry.Kind,
                    TypeName = ifaceMember.ContainingType.ToDisplayString(),
                    MethodName = entry.MethodName,
                    ArgumentTypes = entry.ArgumentTypes,
                    PropertyName = entry.PropertyName,
                    FieldName = entry.FieldName,
                    EventName = entry.EventName,
                };
                TryAddMatch(ifaceEntry, node);
                if (results.Count >= limit) break;
            }
        }

        void TryAddMatch(QueryEntry entry, SyntaxNode node)
        {
            if (!string.Equals(entry.TypeName, typeName, StringComparison.Ordinal)) return;
            if (predicate is not null && !predicate(entry)) return;

            var section = SectionResolver.Resolve(node);
            var lineSpan = node.GetLocation().GetLineSpan();
            var matchLine = lineSpan.StartLinePosition.Line + 1;

            var scope = new UsageScope(
                _currentTypeName!,
                _currentFilePath,
                _currentMethodName,
                _currentMethodDefinitionRange,
                _currentMethodFullRange,
                section,
                matchLine);

            results.Add(new UsageMatch(entry, scope));
        }

        static ISymbol? TryGetMemberSymbol(SyntaxNode node, SemanticModel model, QueryEntry entry)
        {
            if (entry.Kind is not (UsageKind.MethodCall or UsageKind.PropertyRead or UsageKind.PropertyWrite
                or UsageKind.EventSubscribe or UsageKind.EventUnsubscribe or UsageKind.EventCall))
                return null;

            // For event assignment (+=/-=) — resolve Left symbol
            if (node is AssignmentExpressionSyntax assign)
                return model.GetSymbolInfo(assign.Left).Symbol;

            // For EventCall via ?.Invoke() — resolve the event symbol from the conditional access expression
            if (entry.Kind == UsageKind.EventCall && node is InvocationExpressionSyntax inv)
            {
                var conditionalAccess = inv.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>();
                if (conditionalAccess is not null)
                    return model.GetSymbolInfo(conditionalAccess.Expression).Symbol;

                // Direct invocation: Event(args)
                if (inv.Expression is IdentifierNameSyntax id)
                    return model.GetSymbolInfo(id).Symbol;
            }

            return model.GetSymbolInfo(node).Symbol;
        }
    }

    // ── Scope frame ───────────────────────────────────────────────────────────

    readonly record struct ScopeFrame(string? TypeName, string? MethodName, LineRange? MethodDefinitionRange, LineRange? MethodFullRange);
}
