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

        // Phase 1: Collect all source-defined types
        var allTypes = typeCollector.CollectSourceTypes(solution.Compilations);

        var interfaces = allTypes.Where(t => t.Symbol.TypeKind == TypeKind.Interface).ToList();
        var classes = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Class && !t.Symbol.IsAbstract && !t.Symbol.IsStatic)
            .ToList();

        // Phase 2: Build implementation -> abstraction mapping
        var implToAbstractions = new Dictionary<string, List<string>>();
        var abstractionImplementors = new Dictionary<string, List<string>>();

        foreach (var entry in classes)
        {
            var implName = entry.Symbol.ToDisplayString();
            var ifaceNames = typeCollector.GetAllImplementedAbstractions(entry.Symbol);
            implToAbstractions[implName] = ifaceNames;

            foreach (var ifaceName in ifaceNames)
            {
                if (!abstractionImplementors.TryGetValue(ifaceName, out var list))
                {
                    list = [];
                    abstractionImplementors[ifaceName] = list;
                }
                list.Add(implName);
            }
        }

        // Phase 3: Build abstractions dictionary
        var abstractions = BuildAbstractions(
            interfaces, classes, allTypes, abstractionImplementors);

        // Phase 4: Analyze implementations
        var (implementations, standaloneClassCandidates) = await AnalyzeImplementationsAsync(
            classes, implToAbstractions, abstractions, ct);

        // Phase 5: Add standalone classes to abstractions
        await AddStandaloneClassesAsync(
            standaloneClassCandidates, allTypes, abstractions, implementations,
            abstractionImplementors, ct);

        // Phase 6: Add IOptions<T> types as abstractions
        AddOptionsAbstractions(implementations, allTypes, abstractions);

        // Phase 7: Add external (NuGet) abstractions — deps not found in source, SourceFilePath = null
        AddExternalAbstractions(implementations, solution.Compilations, abstractions);

        return new DependencyMapResult(abstractions, implementations);
    }

    Dictionary<string, AbstractionInfo> BuildAbstractions(
        List<SourceType> interfaces,
        List<SourceType> classes,
        List<SourceType> allTypes,
        Dictionary<string, List<string>> abstractionImplementors)
    {
        var abstractions = new Dictionary<string, AbstractionInfo>();

        foreach (var entry in interfaces)
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (typeCollector.IsExcludedInterface(fullName)) continue;
            if (!abstractionImplementors.ContainsKey(fullName)) continue;

            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, abstractionImplementors);
        }

        foreach (var (ifaceName, implementors) in abstractionImplementors)
        {
            if (abstractions.ContainsKey(ifaceName)) continue;
            if (typeCollector.IsExcludedInterface(ifaceName)) continue;

            var ifaceSymbol = abstractionExtractor.FindClosedGenericInterface(ifaceName, classes);
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

        return abstractions;
    }

    async Task<(Dictionary<string, ImplementationInfo>, HashSet<string>)> AnalyzeImplementationsAsync(
        List<SourceType> classes,
        Dictionary<string, List<string>> implToAbstractions,
        Dictionary<string, AbstractionInfo> abstractions,
        CancellationToken ct)
    {
        var implementations = new Dictionary<string, ImplementationInfo>();
        var standaloneClassCandidates = new HashSet<string>();

        foreach (var entry in classes)
        {
            var implName = entry.Symbol.ToDisplayString();
            var ctorDeps = constructorAnalyzer.AnalyzeDependencies(entry.Symbol);
            var baseClasses = typeCollector.GetBaseClassChain(entry.Symbol);
            var memberUsages = await memberUsageAnalyzer.AnalyzeUsagesAsync(
                entry.Symbol, ctorDeps, entry.Compilation, ct);
            var ifaceNames = implToAbstractions.GetValueOrDefault(implName, []);

            implementations[implName] = new ImplementationInfo(
                FullName: implName,
                Namespace: entry.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
                ProjectName: entry.ProjectName,
                SourceFilePath: GetSourcePath(entry.Symbol),
                ImplementedAbstractions: ifaceNames,
                BaseClasses: baseClasses,
                Dependencies: ctorDeps,
                DependencyMemberUsages: memberUsages);

            foreach (var dep in ctorDeps)
            {
                if (!dep.IsOptions && !abstractions.ContainsKey(dep.TypeFullName))
                    standaloneClassCandidates.Add(dep.TypeFullName);
            }

            if (ifaceNames.Count == 0)
                standaloneClassCandidates.Add(implName);
        }

        return (implementations, standaloneClassCandidates);
    }

    async Task AddStandaloneClassesAsync(
        HashSet<string> candidates,
        List<SourceType> allTypes,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, ImplementationInfo> implementations,
        Dictionary<string, List<string>> abstractionImplementors,
        CancellationToken ct)
    {
        foreach (var candidate in candidates)
        {
            if (abstractions.ContainsKey(candidate)) continue;

            var found = allTypes.FirstOrDefault(t => t.Symbol.ToDisplayString() == candidate);
            if (found is null) continue;

            abstractions[candidate] = abstractionExtractor.BuildAbstractionInfo(
                found.Symbol, found.ProjectName, abstractionImplementors);

            if (!implementations.ContainsKey(candidate) && found.Symbol.TypeKind == TypeKind.Class)
            {
                var ctorDeps = constructorAnalyzer.AnalyzeDependencies(found.Symbol);
                var baseClasses = typeCollector.GetBaseClassChain(found.Symbol);
                var memberUsages = await memberUsageAnalyzer.AnalyzeUsagesAsync(
                    found.Symbol, ctorDeps, found.Compilation, ct);

                implementations[candidate] = new ImplementationInfo(
                    FullName: candidate,
                    Namespace: found.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
                    ProjectName: found.ProjectName,
                    SourceFilePath: GetSourcePath(found.Symbol),
                    ImplementedAbstractions: [],
                    BaseClasses: baseClasses,
                    Dependencies: ctorDeps,
                    DependencyMemberUsages: memberUsages);

                if (!abstractionImplementors.ContainsKey(candidate))
                    abstractionImplementors[candidate] = [candidate];
                else if (!abstractionImplementors[candidate].Contains(candidate))
                    abstractionImplementors[candidate].Add(candidate);

                abstractions[candidate] = abstractions[candidate] with
                {
                    Implementations = abstractionImplementors.GetValueOrDefault(candidate, [])
                };
            }
        }
    }

    void AddOptionsAbstractions(
        Dictionary<string, ImplementationInfo> implementations,
        List<SourceType> allTypes,
        Dictionary<string, AbstractionInfo> abstractions)
    {
        foreach (var impl in implementations.Values)
        {
            foreach (var dep in impl.Dependencies.Where(d => d.IsOptions))
            {
                if (abstractions.ContainsKey(dep.TypeFullName)) continue;

                var found = allTypes.FirstOrDefault(t => t.Symbol.ToDisplayString() == dep.TypeFullName);
                if (found is null) continue;

                abstractions[dep.TypeFullName] = new AbstractionInfo(
                    FullName: dep.TypeFullName,
                    Namespace: found.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
                    ProjectName: found.ProjectName,
                    SourceFilePath: GetSourcePath(found.Symbol),
                    IsInterface: false,
                    DeclaredMembers: abstractionExtractor.GetDeclaredMembers(found.Symbol),
                    Implementations: [dep.TypeFullName]);
            }
        }
    }

    /// <summary>
    /// Adds abstractions for NuGet/external dependencies that have no source file.
    /// These are injected types whose symbol exists in referenced assemblies but not in source.
    /// SourceFilePath is null for all of them, which lets ProjectDesignService exclude them from groups
    /// while still resolving them as dependency targets.
    /// </summary>
    void AddExternalAbstractions(
        Dictionary<string, ImplementationInfo> implementations,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations,
        Dictionary<string, AbstractionInfo> abstractions)
    {
        foreach (var impl in implementations.Values)
        {
            foreach (var dep in impl.Dependencies)
            {
                if (dep.IsOptions) continue;
                if (abstractions.ContainsKey(dep.TypeFullName)) continue;
                if (typeCollector.IsExcludedInterface(dep.TypeFullName)) continue;

                // Try to resolve the symbol from any compilation's referenced assemblies
                INamedTypeSymbol? symbol = null;
                foreach (var (_, compilation) in compilations)
                {
                    symbol = compilation.GetTypeByMetadataName(dep.TypeFullName);
                    if (symbol is not null) break;
                }

                if (symbol is null) continue;

                // Only add if it truly has no source (NuGet/framework type)
                if (symbol.DeclaringSyntaxReferences.Length > 0) continue;

                abstractions[dep.TypeFullName] = new AbstractionInfo(
                    FullName: dep.TypeFullName,
                    Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
                    ProjectName: symbol.ContainingAssembly?.Name ?? "",
                    SourceFilePath: null,
                    IsInterface: symbol.TypeKind == TypeKind.Interface,
                    DeclaredMembers: abstractionExtractor.GetDeclaredMembers(symbol),
                    Implementations: []);
            }
        }
    }

    static string? GetSourcePath(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
}