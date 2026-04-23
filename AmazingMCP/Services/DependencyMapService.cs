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
    IMemoryCache cache) : IDependencyMapService
{
    static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public async Task<DependencyMapResult> BuildMapAsync(
        string solutionPath, CancellationToken ct = default)
    {
        var cacheKey = $"depmap:{Path.GetFullPath(solutionPath)}";

        if (cache.TryGetValue<DependencyMapResult>(cacheKey, out var cached))
            return cached!;

        var result = await BuildMapCoreAsync(solutionPath, ct);

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Expiration
        });

        return result;
    }

    async Task<DependencyMapResult> BuildMapCoreAsync(string solutionPath, CancellationToken ct)
    {
        var solution = await workspaceProvider.GetSolutionAsync(solutionPath, ct);
        var compilations = TestProjectFilter.ExcludeTestProjects(solution.Compilations, solution);

        // Phase 1: collect all source-defined types, deduplicate partial classes.
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

        // Phase 3: scan bodies of all concrete + abstract source types
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

        // Post-processing: build ClosedToOpenGenericMap and add missing open generics
        var closedToOpenMap = BuildClosedToOpenGenericMap(abstractions, implementations);

        return new DependencyMapResult(abstractions, implementations, closedToOpenMap);
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

        foreach (var entry in allTypes.Where(t => t.Symbol.TypeKind == TypeKind.Interface))
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (abstractions.ContainsKey(fullName)) continue;

            var implementors = abstractionImplementors.GetValueOrDefault(fullName, []);
            if (implementors.Count == 0) continue;

            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, implementors);
        }

        foreach (var (ifaceName, implementors) in abstractionImplementors)
        {
            if (abstractions.ContainsKey(ifaceName)) continue;

            var ifaceSymbol = abstractionExtractor.FindClosedGenericInterface(ifaceName, concreteClasses);
            if (ifaceSymbol is null) continue;

            var projName = abstractionExtractor.ResolveProjectForClosedGeneric(ifaceSymbol, allTypes);
            abstractions[ifaceName] = abstractionExtractor.BuildAbstractionInfo(
                ifaceSymbol, projName, implementors);
        }

        foreach (var entry in allTypes.Where(t => t.Symbol.TypeKind == TypeKind.Class && t.Symbol.IsAbstract))
        {
            var fullName = entry.Symbol.ToDisplayString();
            if (abstractions.ContainsKey(fullName)) continue;

            var implementors = abstractionImplementors.GetValueOrDefault(fullName, []);
            abstractions[fullName] = abstractionExtractor.BuildAbstractionInfo(
                entry.Symbol, entry.ProjectName, implementors);
        }

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

            if (rawUsages.Count == 0 && !isImplementorOfSomething && !isKnownAbstraction) continue;

            // Register all discovered dependency types as abstractions
            foreach (var raw in rawUsages)
                EnsureAbstraction(raw.TypeInfo, raw.Usage.IsStatic,
                    typeIndex, compilations, abstractions, abstractionImplementors);

            var baseClasses = typeCollector.GetBaseClassChain(entry.Symbol);

            implementations[fullName] = new ImplementationInfo(
                FullName: fullName,
                Namespace: entry.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
                ProjectName: entry.ProjectName,
                SourceFilePath: GetSourcePath(entry.Symbol),
                ImplementedAbstractions: implementedAbstractions,
                BaseClasses: baseClasses,
                Dependencies: rawUsages.Select(r => r.Usage).ToList(),
                IsGeneric: entry.Symbol.IsGenericType);

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
        RawTypeInfo typeInfo,
        bool isStatic,
        Dictionary<string, SourceType> typeIndex,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations,
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, List<string>> abstractionImplementors)
    {
        if (abstractions.ContainsKey(typeInfo.FullName)) return;

        // Source-defined type — use typeIndex for authoritative info
        if (typeIndex.TryGetValue(typeInfo.FullName, out var sourceType))
        {
            var implementors = abstractionImplementors.GetValueOrDefault(typeInfo.FullName, []);
            abstractions[typeInfo.FullName] = abstractionExtractor.BuildAbstractionInfo(
                sourceType.Symbol, sourceType.ProjectName, implementors);
            return;
        }

        // External/NuGet type — RawTypeInfo already has all metadata from the Roslyn symbol,
        // built at scan time. No further symbol lookup needed for the common case.
        if (typeInfo.AssemblyName.Length > 0)
        {
            abstractions[typeInfo.FullName] = abstractionExtractor.BuildAbstractionInfo(
                typeInfo with { IsStaticClass = isStatic || typeInfo.IsStaticClass },
                typeInfo.AssemblyName,
                []);
            return;
        }

        // Last resort: resolve symbol from compilations (handles edge cases where AssemblyName is empty)
        var externalSymbol = FindExternalSymbol(typeInfo, compilations);
        if (externalSymbol is not null)
        {
            var resolved = RawTypeInfo.From(externalSymbol);
            abstractions[typeInfo.FullName] = abstractionExtractor.BuildAbstractionInfo(
                resolved with { IsStaticClass = isStatic || resolved.IsStaticClass },
                resolved.AssemblyName,
                []);
        }
    }

    static INamedTypeSymbol? FindExternalSymbol(
        RawTypeInfo typeInfo,
        IReadOnlyList<(string ProjectName, Compilation Compilation)> compilations)
    {
        // Try direct metadata name first (works for non-generic and open generic types)
        foreach (var (_, compilation) in compilations)
        {
            var symbol = compilation.GetTypeByMetadataName(typeInfo.FullName);
            if (symbol is not null && symbol.DeclaringSyntaxReferences.Length == 0)
                return symbol;
        }

        // For closed generic types: use the pre-computed open generic metadata name
        if (typeInfo.OpenGenericMetadataName is not null)
        {
            foreach (var (_, compilation) in compilations)
            {
                var symbol = compilation.GetTypeByMetadataName(typeInfo.OpenGenericMetadataName);
                if (symbol is not null && symbol.DeclaringSyntaxReferences.Length == 0)
                    return symbol;
            }
        }

        return null;
    }

    // ─── Post-processing: ClosedToOpenGenericMap ─────────────────────────────

    /// <summary>
    /// Builds a map of closed generic abstractions → open generic full names.
    /// A closed generic abstraction is "collapsed" into its open generic if it has no
    /// source-defined implementations (i.e. no class explicitly implements it).
    /// Missing open generic abstractions are added to the abstractions dictionary.
    /// </summary>
    static IReadOnlyDictionary<string, string> BuildClosedToOpenGenericMap(
        Dictionary<string, AbstractionInfo> abstractions,
        Dictionary<string, ImplementationInfo> implementations)
    {
        var closedToOpen = new Dictionary<string, string>();
        // Collect missing open generics separately to avoid modifying dict while iterating
        var missingOpenGenerics = new Dictionary<string, AbstractionInfo>();

        foreach (var (key, abstraction) in abstractions)
        {
            var openGenericFullName = abstraction.OpenGenericFullName;
            if (openGenericFullName is null) continue;

            // Only collapse if no source-defined class explicitly implements this closed generic
            // AND no generic implementation covers it (e.g. class Repo<T> : IRepo<T>)
            if (abstraction.Implementations.Count > 0) continue;

            // Check if any implementation is generic and could cover this abstraction
            // (e.g. class Repository<TEntity> : IRepository<TEntity> covers IRepository<Animal>)
            // This is already handled: if such a class exists, it would be in abstractionImplementors
            // and thus Implementations would not be empty. So Implementations.Count == 0 is sufficient.

            closedToOpen[key] = openGenericFullName;

            // Ensure open generic exists in abstractions
            if (!abstractions.ContainsKey(openGenericFullName)
                && !missingOpenGenerics.ContainsKey(openGenericFullName))
            {
                missingOpenGenerics[openGenericFullName] = new AbstractionInfo
                {
                    FullName = openGenericFullName,
                    Namespace = abstraction.Namespace,
                    ProjectName = abstraction.ProjectName,
                    SourceFilePath = abstraction.SourceFilePath,
                    IsInterface = abstraction.IsInterface,
                    IsAbstractClass = abstraction.IsAbstractClass,
                    IsStaticClass = abstraction.IsStaticClass,
                    Implementations = [],
                    OpenGenericFullName = null
                };
            }
        }

        foreach (var (key, value) in missingOpenGenerics)
            abstractions[key] = value;

        return closedToOpen;
    }

    static string? GetSourcePath(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
}
