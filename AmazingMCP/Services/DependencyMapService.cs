using AmazingMCP.Models;
using AmazingMCP.Services.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace AmazingMCP.Services;

public class DependencyMapService(
    IWorkspaceProvider workspaceProvider,
    ITypeCollector typeCollector,
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
        var compilations = TestProjectFilter.ExcludeTestProjects(solution.Compilations, solution);

        // Phase 1: collect all source-defined types, deduplicate partial classes.
        // When a type appears in multiple compilations (via project references),
        // prefer the compilation that actually owns the syntax tree.
        var allTypes = typeCollector.CollectSourceTypes(compilations)
            .GroupBy(t => t.Symbol.ToDisplayString())
            .Select(g => g.FirstOrDefault(t =>
                t.Symbol.DeclaringSyntaxReferences.Any(r => t.Compilation.ContainsSyntaxTree(r.SyntaxTree)))
                ?? g.First())
            .ToList();

        var typeIndex = allTypes.ToDictionary(t => t.Symbol.ToDisplayString());

        // Phase 2: build initial abstraction set from type declarations
        var abstractions = new Dictionary<string, AbstractionInfo>();
        var abstractionImplementors = new Dictionary<string, List<string>>();

        CollectInitialAbstractions(allTypes, typeIndex, abstractions, abstractionImplementors);

        // Phase 3: scan bodies of all concrete + abstract source types,
        // discover dependencies inline, add missing abstractions on the fly
        var implementations = new Dictionary<string, ImplementationInfo>();

        await AnalyzeAllTypeBodiesAsync(
            allTypes, typeIndex, compilations,
            abstractions, abstractionImplementors,
            implementations, ct);

        // Sync implementations lists into abstractions
        foreach (var (abstractionName, implList) in abstractionImplementors)
        {
            if (!abstractions.TryGetValue(abstractionName, out var abs)) continue;
            abstractions[abstractionName] = abs with { Implementations = implList };
        }

        return new DependencyMapResult(abstractions, implementations);
    }

    // ─── Phase 2: initial abstraction collection ────────────────────────────

    void CollectInitialAbstractions(
        List<SourceType> allTypes,
        Dictionary<string, SourceType> typeIndex,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors)
    {
        var concreteClasses = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Class && !t.Symbol.IsAbstract && !t.Symbol.IsStatic)
            .ToList();

        // Build implementors map from concrete classes
        foreach (var entry in concreteClasses)
        {
            var implName = entry.Symbol.ToDisplayString();
            foreach (var abstractionName in typeCollector.GetAllImplementedAbstractions(entry.Symbol))
            {
                if (!abstractionImplementors.TryGetValue(abstractionName, out var list))
                    abstractionImplementors[abstractionName] = list = [];
                if (!list.Contains(implName))
                    list.Add(implName);
            }
        }

        // Interfaces with at least one source implementor
        foreach (var entry in allTypes.Where(t => t.Symbol.TypeKind == TypeKind.Interface))
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (abstractions.ContainsKey(fullName)) continue;

            var implementors = abstractionImplementors.GetValueOrDefault(fullName, []);
            if (implementors.Count == 0) continue;

            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, implementors);
        }

        // Closed generic interfaces implemented by source classes
        foreach (var (ifaceName, implementors) in abstractionImplementors)
        {
            if (abstractions.ContainsKey(ifaceName)) continue;

            var ifaceSymbol = abstractionExtractor.FindClosedGenericInterface(ifaceName, concreteClasses);
            if (ifaceSymbol is null) continue;

            var projName = abstractionExtractor.ResolveProjectForClosedGeneric(ifaceSymbol, allTypes);
            abstractions[ifaceName] = abstractionExtractor.BuildAbstractionInfo(
                ifaceSymbol, projName, implementors);
        }

        // Abstract classes
        foreach (var entry in allTypes.Where(t => t.Symbol.TypeKind == TypeKind.Class && t.Symbol.IsAbstract))
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (abstractions.ContainsKey(fullName)) continue;

            var implementors = abstractionImplementors.GetValueOrDefault(fullName, []);
            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, implementors);
        }

        // Base classes in the inheritance chain of concrete classes
        foreach (var entry in concreteClasses)
        {
            foreach (var baseClassName in typeCollector.GetBaseClassChain(entry.Symbol))
            {
                if (abstractions.ContainsKey(baseClassName)) continue;
                if (!typeIndex.TryGetValue(baseClassName, out var baseEntry)) continue;

                var implementors = abstractionImplementors.GetValueOrDefault(baseClassName, []);
                abstractions[baseClassName] = abstractionExtractor.BuildAbstractionInfo(
                    baseEntry.Symbol, baseEntry.ProjectName, implementors);
            }
        }
    }

    // ─── Phase 3: scan all type bodies ──────────────────────────────────────

    async Task AnalyzeAllTypeBodiesAsync(
        List<SourceType> allTypes,
        Dictionary<string, SourceType> typeIndex,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors,
        Dictionary<string, ImplementationInfo> implementations,
        CancellationToken ct)
    {
        // Scan concrete classes + abstract classes (they have bodies with dependencies)
        var toScan = allTypes
            .Where(t => t.Symbol.TypeKind == TypeKind.Class && !t.Symbol.IsStatic)
            .ToList();

        foreach (var entry in toScan)
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (implementations.ContainsKey(fullName)) continue;

            var rawUsages = await memberUsageAnalyzer.AnalyzeAsync(
                entry.Symbol, entry.Compilation, ct);

            var implementedAbstractions = typeCollector.GetAllImplementedAbstractions(entry.Symbol);
            var isImplementorOfSomething = implementedAbstractions.Count > 0
                || abstractionImplementors.Values.Any(list => list.Contains(fullName));
            var isKnownAbstraction = abstractions.ContainsKey(fullName);

            // Skip if no usages AND not an implementor AND not already a known abstraction
            if (rawUsages.Count == 0 && !isImplementorOfSomething && !isKnownAbstraction) continue;

            // Register all discovered dependency types as abstractions
            foreach (var usage in rawUsages)
                EnsureAbstraction(usage.AbstractionFullName, usage.IsStatic,
                    typeIndex, compilations, abstractions, abstractionImplementors);

            var baseClasses = typeCollector.GetBaseClassChain(entry.Symbol);

            implementations[fullName] = new ImplementationInfo(
                FullName: fullName,
                Namespace: entry.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
                ProjectName: entry.ProjectName,
                SourceFilePath: GetSourcePath(entry.Symbol),
                ImplementedAbstractions: implementedAbstractions,
                BaseClasses: baseClasses,
                Dependencies: rawUsages);

            // Standalone class (no interface): register as its own abstraction.
            // If pre-registered by EnsureAbstraction with empty Implementations, fix it.
            if (!isImplementorOfSomething && rawUsages.Count > 0)
            {
                if (!abstractions.TryGetValue(fullName, out var existing))
                    abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                        entry.Symbol, entry.ProjectName, [fullName]);
                else if (existing.Implementations.Count == 0)
                    abstractions[fullName] = existing with { Implementations = [fullName] };
            }
        }
    }

    void EnsureAbstraction(
        string typeName,
        bool isStatic,
        Dictionary<string, SourceType> typeIndex,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors)
    {
        if (abstractions.ContainsKey(typeName)) return;

        // Source-defined type
        if (typeIndex.TryGetValue(typeName, out var sourceType))
        {
            var implementors = abstractionImplementors.GetValueOrDefault(typeName, []);
            abstractions[typeName] = abstractionExtractor.BuildAbstractionInfo(
                sourceType.Symbol, sourceType.ProjectName, implementors);
            return;
        }

        // External/NuGet type — resolve symbol from compilations
        var externalSymbol = ResolveExternalSymbol(typeName, compilations);
        if (externalSymbol is not null)
        {
            abstractions[typeName] = new AbstractionInfo(
                FullName: typeName,
                Namespace: externalSymbol.ContainingNamespace?.ToDisplayString() ?? "",
                ProjectName: externalSymbol.ContainingAssembly?.Name ?? "",
                SourceFilePath: null,
                IsInterface: externalSymbol.TypeKind == TypeKind.Interface,
                IsAbstractClass: externalSymbol.TypeKind == TypeKind.Class && externalSymbol.IsAbstract,
                IsStaticClass: isStatic || (externalSymbol.TypeKind == TypeKind.Class && externalSymbol.IsStatic),
                Implementations: []);
        }
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
