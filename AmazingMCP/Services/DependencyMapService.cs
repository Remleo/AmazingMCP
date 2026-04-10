using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace AmazingMCP.Services;

public class DependencyMapService(
    IWorkspaceProvider workspaceProvider,
    ITypeCollector typeCollector,
    IConstructorAnalyzer constructorAnalyzer,
    IMemberUsageAnalyzer memberUsageAnalyzer,
    IAbstractionExtractor abstractionExtractor,
    IMemoryCache cache)
{
    static readonly TimeSpan SlidingExpiration = TimeSpan.FromHours(2);

    public async Task<DependencyMapResult> BuildMapAsync(
        string solutionPath, CancellationToken ct = default)
    {
        var cacheKey = $"depmap:{Path.GetFullPath(solutionPath)}";

        if (cache.TryGetValue<DependencyMapResult>(cacheKey, out var cached))
            return cached!;

        var result = await BuildMapCoreAsync(solutionPath, ct);

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });

        return result;
    }

    async Task<DependencyMapResult> BuildMapCoreAsync(string solutionPath, CancellationToken ct)
    {
        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);

        // Phase 1: collect all source-defined types, excluding test projects.
        // Deduplicate by full name — partial classes across multiple files produce multiple SourceType entries
        // with the same symbol display string, which would cause duplicate key errors downstream.
        var compilations = TestProjectFilter.ExcludeTestProjects(solution.Compilations, solution);
        var allTypes = typeCollector.CollectSourceTypes(compilations)
            .GroupBy(t => t.Symbol.ToDisplayString())
            .Select(g => g.First())
            .ToList();
        var typeIndex = allTypes.ToDictionary(t => t.Symbol.ToDisplayString());

        // Phase 2: determine initial abstraction set
        var abstractions = new Dictionary<string, AbstractionInfo>();
        var abstractionImplementors = new Dictionary<string, List<string>>();

        CollectInitialAbstractions(allTypes, typeIndex, abstractions, abstractionImplementors);

        // Phase 3: find implementations for each abstraction, analyze constructor deps & member usages
        var implementations = new Dictionary<string, ImplementationInfo>();
        var missingAbstractions = new HashSet<string>();

        await AnalyzeImplementationsAsync(
            allTypes, abstractions, abstractionImplementors,
            implementations, missingAbstractions, compilations, ct);

        // Phase 4: iteratively resolve missing abstractions
        await ResolveMissingAbstractionsAsync(
            missingAbstractions, typeIndex, abstractions, abstractionImplementors,
            implementations, compilations, ct);

        return new DependencyMapResult(abstractions, implementations);
    }

    // ─── Phase 2: initial abstraction collection ────────────────────────────

    void CollectInitialAbstractions(
        List<SourceType> allTypes,
        Dictionary<string, SourceType> typeIndex,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors)
    {
        // Pass 1: collect all interfaces that have at least one source-defined implementor
        // and all concrete classes that qualify as abstractions.
        // We need two passes: first build implementors map, then build AbstractionInfo.

        var interfaces = allTypes.Where(t => t.Symbol.TypeKind == TypeKind.Interface).ToList();
        var concreteClasses = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Class && !t.Symbol.IsAbstract && !t.Symbol.IsStatic)
            .ToList();
        var abstractClasses = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Class && t.Symbol.IsAbstract && !t.Symbol.IsStatic)
            .ToList();

        // Build implementors map: abstraction full name → list of concrete implementor full names
        foreach (var entry in concreteClasses)
        {
            var implName = entry.Symbol.ToDisplayString();
            var implemented = typeCollector.GetAllImplementedAbstractions(entry.Symbol);
            foreach (var ifaceName in implemented)
            {
                if (!abstractionImplementors.TryGetValue(ifaceName, out var list))
                    abstractionImplementors[ifaceName] = list = [];
                if (!list.Contains(implName))
                    list.Add(implName);
            }
        }

        // Interfaces: add if they have at least one source implementor
        foreach (var entry in interfaces)
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (typeCollector.IsExcludedInterface(fullName)) continue;
            if (!abstractionImplementors.ContainsKey(fullName)) continue;

            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, abstractionImplementors);
        }

        // Closed generic interfaces (implemented by source classes but not directly in interfaces list)
        foreach (var (ifaceName, implementors) in abstractionImplementors)
        {
            if (abstractions.ContainsKey(ifaceName)) continue;
            if (typeCollector.IsExcludedInterface(ifaceName)) continue;

            var ifaceSymbol = abstractionExtractor.FindClosedGenericInterface(ifaceName, concreteClasses);
            if (ifaceSymbol is null) continue;

            var projName = abstractionExtractor.ResolveProjectForClosedGeneric(ifaceSymbol, allTypes);
            abstractions[ifaceName] = new AbstractionInfo(
                FullName: ifaceName,
                Namespace: ifaceSymbol.ContainingNamespace?.ToDisplayString() ?? "",
                ProjectName: projName,
                SourceFilePath: GetSourcePath(ifaceSymbol.OriginalDefinition),
                IsInterface: true,
                DeclaredMembers: abstractionExtractor.GetDeclaredMembers(ifaceSymbol),
                Implementations: implementors);
        }

        // Abstract classes: each is an abstraction on its own
        foreach (var entry in abstractClasses)
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (abstractions.ContainsKey(fullName)) continue;
            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, abstractionImplementors);
        }

        // Concrete classes that qualify as standalone abstractions:
        // - no interfaces (even through base chain), AND
        // - has a constructor with at least one complex dependency
        foreach (var entry in concreteClasses)
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (abstractions.ContainsKey(fullName)) continue;

            var implemented = abstractionImplementors.Keys
                .Any(k => abstractionImplementors[k].Contains(fullName));
            if (implemented) continue; // it's an implementation of something

            var hasInterfaceInChain = typeCollector.GetAllImplementedAbstractions(entry.Symbol).Count > 0;
            if (hasInterfaceInChain) continue;

            var deps = constructorAnalyzer.AnalyzeDependencies(entry.Symbol);
            if (deps.Count == 0) continue; // no complex deps → not an abstraction

            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, abstractionImplementors);

            if (!abstractionImplementors.ContainsKey(fullName))
                abstractionImplementors[fullName] = [fullName];
        }

        // IOptions<T> types: add as abstractions when referenced
        // (deferred to Phase 4 since we need to scan constructors first)
    }

    // ─── Phase 3: analyze implementations ───────────────────────────────────

    async Task AnalyzeImplementationsAsync(
        List<SourceType> allTypes,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors,
        Dictionary<string, ImplementationInfo> implementations,
        HashSet<string> missingAbstractions,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations,
        CancellationToken ct)
    {
        // Analyze every concrete class that is an implementor of at least one known abstraction,
        // plus standalone abstractions (which are their own implementors).
        var toAnalyze = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Class && !t.Symbol.IsAbstract && !t.Symbol.IsStatic)
            .ToList();

        foreach (var entry in toAnalyze)
        {
            var fullName = entry.Symbol.ToDisplayString();

            // Only analyze if it's an implementor of something OR a standalone abstraction
            var isImplementor = abstractionImplementors.Values.Any(list => list.Contains(fullName));
            var isStandaloneAbstraction = abstractions.ContainsKey(fullName);
            if (!isImplementor && !isStandaloneAbstraction) continue;

            if (implementations.ContainsKey(fullName)) continue;

            var ctorDeps = constructorAnalyzer.AnalyzeDependencies(entry.Symbol);
            var baseClasses = typeCollector.GetBaseClassChain(entry.Symbol);
            var memberUsages = await memberUsageAnalyzer.AnalyzeUsagesAsync(
                entry.Symbol, ctorDeps, entry.Compilation, ct);

            var implementedAbstractions = typeCollector.GetAllImplementedAbstractions(entry.Symbol);

            implementations[fullName] = new ImplementationInfo(
                FullName: fullName,
                Namespace: entry.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
                ProjectName: entry.ProjectName,
                SourceFilePath: GetSourcePath(entry.Symbol),
                ImplementedAbstractions: implementedAbstractions,
                BaseClasses: baseClasses,
                Dependencies: ctorDeps,
                DependencyMemberUsages: memberUsages.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<MemberUsage>)kv.Value));

            // Collect missing abstractions from dependencies
            foreach (var dep in ctorDeps)
            {
                if (dep.IsOptions)
                {
                    // IOptions<T> → T is an abstraction
                    if (!abstractions.ContainsKey(dep.TypeFullName))
                        missingAbstractions.Add(dep.TypeFullName);
                }
                else if (!abstractions.ContainsKey(dep.TypeFullName))
                {
                    missingAbstractions.Add(dep.TypeFullName);
                }
            }
        }
    }

    // ─── Phase 4: resolve missing abstractions iteratively ──────────────────

    async Task ResolveMissingAbstractionsAsync(
        HashSet<string> missingAbstractions,
        Dictionary<string, SourceType> typeIndex,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors,
        Dictionary<string, ImplementationInfo> implementations,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations,
        CancellationToken ct)
    {
        var queue = new Queue<string>(missingAbstractions);
        var visited = new HashSet<string>(abstractions.Keys);

        while (queue.Count > 0)
        {
            var typeName = queue.Dequeue();
            if (!visited.Add(typeName)) continue;
            if (abstractions.ContainsKey(typeName)) continue;

            // Try source-defined type first
            if (typeIndex.TryGetValue(typeName, out var sourceType))
            {
                await AddSourceAbstractionAsync(
                    sourceType, typeIndex, abstractions, abstractionImplementors,
                    implementations, queue, ct);
                continue;
            }

            // Try NuGet/external type
            var externalSymbol = ResolveExternalSymbol(typeName, compilations);
            if (externalSymbol is not null)
            {
                AddExternalAbstraction(externalSymbol, typeName, abstractions);
            }
        }
    }

    async Task AddSourceAbstractionAsync(
        SourceType entry,
        Dictionary<string, SourceType> typeIndex,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors,
        Dictionary<string, ImplementationInfo> implementations,
        Queue<string> queue,
        CancellationToken ct)
    {
        var fullName = entry.Symbol.ToDisplayString();

        abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
            entry.Symbol, entry.ProjectName, abstractionImplementors);

        if (implementations.ContainsKey(fullName)) return;
        if (entry.Symbol.IsAbstract || entry.Symbol.TypeKind == TypeKind.Interface) return;

        var ctorDeps = constructorAnalyzer.AnalyzeDependencies(entry.Symbol);
        var baseClasses = typeCollector.GetBaseClassChain(entry.Symbol);
        var memberUsages = await memberUsageAnalyzer.AnalyzeUsagesAsync(
            entry.Symbol, ctorDeps, entry.Compilation, ct);
        var implementedAbstractions = typeCollector.GetAllImplementedAbstractions(entry.Symbol);

        implementations[fullName] = new ImplementationInfo(
            FullName: fullName,
            Namespace: entry.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
            ProjectName: entry.ProjectName,
            SourceFilePath: GetSourcePath(entry.Symbol),
            ImplementedAbstractions: implementedAbstractions,
            BaseClasses: baseClasses,
            Dependencies: ctorDeps,
            DependencyMemberUsages: memberUsages.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<MemberUsage>)kv.Value));

        // Enqueue newly discovered missing deps
        foreach (var dep in ctorDeps)
        {
            if (!abstractions.ContainsKey(dep.TypeFullName))
                queue.Enqueue(dep.TypeFullName);
        }
    }

    void AddExternalAbstraction(
        INamedTypeSymbol symbol,
        string typeName,
        Dictionary<string, AbstractionInfo> abstractions)
    {
        // External (NuGet/framework) types: no source file, no System/Microsoft.Extensions.Options filter
        abstractions[typeName] = new AbstractionInfo(
            FullName: typeName,
            Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
            ProjectName: symbol.ContainingAssembly?.Name ?? "",
            SourceFilePath: null,
            IsInterface: symbol.TypeKind == TypeKind.Interface,
            DeclaredMembers: abstractionExtractor.GetDeclaredMembers(symbol),
            Implementations: []);
    }

    static INamedTypeSymbol? ResolveExternalSymbol(
        string typeName,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations)
    {
        foreach (var (_, compilation) in compilations)
        {
            var symbol = compilation.GetTypeByMetadataName(typeName);
            if (symbol is not null && symbol.DeclaringSyntaxReferences.Length == 0)
                return symbol;
        }
        return null;
    }

    static string? GetSourcePath(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
}
