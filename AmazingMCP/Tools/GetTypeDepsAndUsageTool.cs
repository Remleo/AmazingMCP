using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using AmazingMCP.Models;
using AmazingMCP.Models.Design;
using AmazingMCP.Services;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

// [McpServerToolType] // temporarily disabled
public static class GetTypeDepsAndUsageTool
{
    [McpServerTool(Name = "get_type_deps_and_usage", ReadOnly = true), Description(
        "Look up any type by name to see who implements it, what it depends on, and who uses it. " +
        "Supports exact full name, partial name, and `*` wildcard patterns. " +
        "For each matched abstraction shows: implementations with their full dependency tree and member-level call details; " +
        "and which other types use this abstraction. " +
        "Ideal for impact analysis, understanding a specific interface, or tracing a dependency chain.")]
    public static async Task<string> GetTypeDepsAndUsage(
        IDependencyMapService dependencyMapService,
        IDependencyAggregator dependencyAggregator,
        ISolutionResolver solutionResolver,
        IWildcardPatternFactory wildcardFactory,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description("Type query: full name, partial name, or '*' wildcard patterns.")] string typeQuery,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null) return error!;

        var depMap = await dependencyMapService.BuildMapAsync(resolved, ct);
        return FormatMarkdown(depMap, typeQuery, wildcardFactory, dependencyAggregator);
    }

    internal static string FormatMarkdown(
        DependencyMapResult depMap,
        string typeQuery,
        IWildcardPatternFactory wildcardFactory,
        IDependencyAggregator? aggregator = null)
    {
        if (typeQuery.Contains('*'))
        {
            var wildcardMatches = FindByWildcard(depMap.Abstractions.Keys, typeQuery, wildcardFactory);
            return wildcardMatches.Count > 0
                ? FormatAbstractionResults(depMap, wildcardMatches, aggregator)
                : $"No types found matching pattern `{typeQuery}`.";
        }

        // Exact match — check abstractions first, then implementations
        if (depMap.Abstractions.ContainsKey(typeQuery))
            return FormatAbstractionResults(depMap, [typeQuery], aggregator);

        if (depMap.Implementations.ContainsKey(typeQuery))
            return FormatImplementationResult(depMap, typeQuery, aggregator);

        return PerformFallbackSearch(depMap, typeQuery, wildcardFactory, aggregator);
    }

    internal static List<string> FindByWildcard(IEnumerable<string> keys, string pattern, IWildcardPatternFactory wildcardFactory)
    {
        var compiled = wildcardFactory.CreateForTypeNames(pattern);
        return keys.Where(k => compiled.IsMatch(k)).OrderBy(k => k).ToList();
    }

    internal static string PerformFallbackSearch(
        DependencyMapResult depMap, string typeQuery, IWildcardPatternFactory wildcardFactory, IDependencyAggregator? aggregator)
    {
        var fuzzyQuery = NormalizeForFuzzySearch(typeQuery);
        var compiled = wildcardFactory.CreateForTypeNames(fuzzyQuery);

        var matchedAbstractions = depMap.Abstractions.Keys
            .Where(k => compiled.IsMatch(k)).OrderBy(k => k).ToList();
        var matchedImplementations = depMap.Implementations.Keys
            .Where(k => compiled.IsMatch(k) && !matchedAbstractions.Contains(k))
            .OrderBy(k => k).ToList();

        if (matchedAbstractions.Count == 0 && matchedImplementations.Count == 0)
            return $"No exact match found for `{typeQuery}`. " +
                   $"Fuzzy search with pattern `{fuzzyQuery}` also returned no results.";

        var sb = new StringBuilder();
        sb.AppendLine($"No exact match found for `{typeQuery}`.");
        sb.AppendLine($"Showing results for fuzzy search pattern `{fuzzyQuery}`:");
        sb.AppendLine();

        if (matchedAbstractions.Count > 0)
            sb.Append(FormatAbstractionResults(depMap, matchedAbstractions, aggregator));

        if (matchedImplementations.Count > 0)
        {
            if (matchedAbstractions.Count > 0) sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Matched implementations");
            sb.AppendLine();

            foreach (var implName in matchedImplementations)
            {
                if (!depMap.Implementations.TryGetValue(implName, out var impl)) continue;

                sb.AppendLine($"### {implName}");

                if (impl.ImplementedAbstractions.Count > 0)
                {
                    sb.AppendLine("Implements:");
                    foreach (var abs in impl.ImplementedAbstractions)
                        sb.AppendLine($"- {abs}");
                }

                var allUsages = aggregator is not null
                    ? aggregator.GetAllUsages(implName, depMap)
                    : impl.Dependencies;

                if (allUsages.Count > 0)
                {
                    sb.AppendLine("Depends on:");
                    foreach (var dep in allUsages)
                        sb.AppendLine($"- {dep.AbstractionFullName}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    internal static string NormalizeForFuzzySearch(string query)
    {
        var normalized = query;

        if (normalized.Contains('<') && normalized.Contains('>'))
        {
            var openIdx = normalized.IndexOf('<');
            var closeIdx = normalized.LastIndexOf('>');
            if (closeIdx > openIdx)
            {
                var genericPart = normalized.Substring(openIdx + 1, closeIdx - openIdx - 1);
                var commaCount = genericPart.Count(c => c == ',');
                var wildcardParams = string.Join(", ", Enumerable.Repeat("*", commaCount + 1));
                normalized = normalized[..openIdx] + "<" + wildcardParams + ">";
            }
        }

        if (!normalized.StartsWith("*")) normalized = "*" + normalized;
        if (!normalized.EndsWith("*")) normalized = normalized + "*";
        return normalized;
    }

    static string FormatImplementationResult(
        DependencyMapResult depMap,
        string implFullName,
        IDependencyAggregator? aggregator)
    {
        if (!depMap.Implementations.TryGetValue(implFullName, out var impl))
            return $"No type found for `{implFullName}`.";

        var sb = new StringBuilder();
        sb.AppendLine($"# {implFullName}");
        sb.AppendLine();

        if (impl.ImplementedAbstractions.Count > 0)
        {
            sb.AppendLine("Implements:");
            foreach (var abs in impl.ImplementedAbstractions)
                sb.AppendLine($"- {abs}");
            sb.AppendLine();
        }

        var allUsages = aggregator is not null
            ? aggregator.GetAllUsages(implFullName, depMap)
            : impl.Dependencies;

        if (allUsages.Count > 0)
        {
            sb.AppendLine("## Depends on");
            sb.AppendLine();
            foreach (var dep in allUsages)
            {
                sb.AppendLine($"- {dep.AbstractionFullName}");
                foreach (var usage in dep.Usages)
                    sb.AppendLine(FormatUsageLine(usage));
            }
            sb.AppendLine();
        }

        // Used by — no collapse needed here (we're looking at a concrete impl, not an abstraction)
        var usedBy = BuildUsedByIndex(depMap)[implFullName];

        if (usedBy.Count > 0)
        {
            sb.AppendLine("## Used by");
            sb.AppendLine();
            AppendUsedByGroups(sb, usedBy, implFullName);
        }

        return sb.ToString().TrimEnd();
    }

    static string FormatAbstractionResults(
        DependencyMapResult depMap,
        List<string> abstractionNames,
        IDependencyAggregator? aggregator)
    {
        // Build collapse structures once for this result set
        var openToClosedIndex = GenericCollapseHelper.BuildOpenToClosedIndex(depMap.ClosedToOpenGenericMap);
        var (finalNames, collapsedCloseds) = GenericCollapseHelper.Collapse(
            abstractionNames, depMap.ClosedToOpenGenericMap, openToClosedIndex);

        // Build used-by index once: abstractionFullName → list of implementations that use it
        var usedByIndex = BuildUsedByIndex(depMap);

        // Track already-printed implementations to avoid repeating full dep lists
        var printedImpls = new HashSet<string>();

        var sb = new StringBuilder();

        foreach (var abstractionFullName in finalNames)
        {
            if (!depMap.Abstractions.TryGetValue(abstractionFullName, out var abstraction)) continue;

            // Collect all effective names: open generic + its collapsed closeds (if any)
            var effectiveNames = GenericCollapseHelper
                .GetEffectiveAbstractionNames(abstractionFullName, collapsedCloseds)
                .ToList();

            sb.AppendLine($"# {abstractionFullName}");
            sb.AppendLine();

            // Implementations: from the abstraction itself + all collapsed closeds
            var allImplNames = effectiveNames
                .SelectMany(n => depMap.Abstractions.TryGetValue(n, out var a) ? a.Implementations : [])
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (allImplNames.Count > 0)
            {
                sb.AppendLine("## Implementations");
                sb.AppendLine();

                foreach (var implName in allImplNames)
                {
                    sb.AppendLine($"### {implName}");

                    if (!depMap.Implementations.TryGetValue(implName, out var impl))
                    {
                        sb.AppendLine();
                        continue;
                    }

                    // If this impl was already printed in full earlier — show a short reference
                    if (!printedImpls.Add(implName))
                    {
                        sb.AppendLine("*(see first occurrence above)*");
                        sb.AppendLine();
                        continue;
                    }

                    var allUsages = aggregator is not null
                        ? aggregator.GetAllUsages(implName, depMap)
                        : impl.Dependencies;

                    if (allUsages.Count == 0)
                    {
                        sb.AppendLine();
                        continue;
                    }

                    sb.AppendLine("Depends on:");
                    foreach (var dep in allUsages)
                    {
                        sb.AppendLine($"- {dep.AbstractionFullName}");
                        foreach (var usage in dep.Usages)
                            sb.AppendLine(FormatUsageLine(usage));
                    }

                    sb.AppendLine();
                }
            }

            // Used by: aggregate across all effective names
            var usedByAll = effectiveNames
                .SelectMany(n => usedByIndex.TryGetValue(n, out var list) ? list : [])
                .DistinctBy(i => i.FullName)
                .ToList();

            if (usedByAll.Count > 0)
            {
                sb.AppendLine("## Used by");
                sb.AppendLine();

                // For "Used by" we show usages for any of the effective abstraction names
                AppendUsedByGroupsMulti(sb, usedByAll, effectiveNames);
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    // ─── Used by helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a reverse index: abstractionFullName → implementations that use it (with usages > 0).
    /// O(n) build.
    /// </summary>
    static IReadOnlyDictionary<string, List<ImplementationInfo>> BuildUsedByIndex(DependencyMapResult depMap)
    {
        var index = new Dictionary<string, List<ImplementationInfo>>();
        foreach (var impl in depMap.Implementations.Values)
        {
            foreach (var dep in impl.Dependencies)
            {
                if (dep.Usages.Count == 0) continue;
                if (!index.TryGetValue(dep.AbstractionFullName, out var list))
                    index[dep.AbstractionFullName] = list = [];
                list.Add(impl);
            }
        }
        return index;
    }

    static void AppendUsedByGroups(
        StringBuilder sb,
        List<ImplementationInfo> usedBy,
        string abstractionFullName)
    {
        var byAbstraction = usedBy
            .SelectMany(i => i.ImplementedAbstractions.Select(a => (Abstraction: a, Impl: i)))
            .GroupBy(x => x.Abstraction)
            .OrderBy(g => g.Key);
        var standalone = usedBy.Where(i => i.ImplementedAbstractions.Count == 0).ToList();

        foreach (var group in byAbstraction)
        {
            sb.AppendLine($"### {group.Key}");
            foreach (var (_, i) in group.OrderBy(x => x.Impl.FullName))
                AppendImplUsages(sb, i, [abstractionFullName]);
            sb.AppendLine();
        }
        if (standalone.Count > 0)
        {
            sb.AppendLine("### (standalone)");
            foreach (var i in standalone.OrderBy(x => x.FullName))
                AppendImplUsages(sb, i, [abstractionFullName]);
        }
    }

    /// <summary>
    /// Like AppendUsedByGroups but collects usages across multiple abstraction names
    /// (open generic + its collapsed closeds).
    /// </summary>
    static void AppendUsedByGroupsMulti(
        StringBuilder sb,
        List<ImplementationInfo> usedBy,
        IReadOnlyList<string> abstractionNames)
    {
        var byAbstraction = usedBy
            .SelectMany(i => i.ImplementedAbstractions.Select(a => (Abstraction: a, Impl: i)))
            .GroupBy(x => x.Abstraction)
            .OrderBy(g => g.Key);
        var standalone = usedBy.Where(i => i.ImplementedAbstractions.Count == 0).ToList();

        foreach (var group in byAbstraction)
        {
            sb.AppendLine($"### {group.Key}");
            foreach (var (_, i) in group.OrderBy(x => x.Impl.FullName))
                AppendImplUsages(sb, i, abstractionNames);
            sb.AppendLine();
        }
        if (standalone.Count > 0)
        {
            sb.AppendLine("### (standalone)");
            foreach (var i in standalone.OrderBy(x => x.FullName))
                AppendImplUsages(sb, i, abstractionNames);
        }
    }

    static void AppendImplUsages(
        StringBuilder sb,
        ImplementationInfo impl,
        IReadOnlyList<string> abstractionNames)
    {
        sb.AppendLine($"- {impl.FullName}");
        // Collect usages across all effective abstraction names (dedup by member)
        var usages = abstractionNames
            .SelectMany(name =>
            {
                var dep = impl.Dependencies.FirstOrDefault(d => d.AbstractionFullName == name);
                return dep?.Usages ?? [];
            })
            .DistinctBy(u => (u.MemberName, u.Kind))
            .ToList();

        foreach (var usage in usages)
            sb.AppendLine(FormatUsageLine(usage));
    }

    // ─── Formatting helpers ──────────────────────────────────────────────────

    static string UsageLabel(MemberUsage usage) => usage.Kind switch
    {
        MemberUsageKind.PropertyGet => $"{usage.MemberName} {{get}}",
        MemberUsageKind.PropertySet => $"{usage.MemberName} {{set}}",
        _ => $"{usage.MemberName}()"
    };

    static string FormatUsageLine(MemberUsage usage) =>
        $"  - {UsageLabel(usage)}";
}
