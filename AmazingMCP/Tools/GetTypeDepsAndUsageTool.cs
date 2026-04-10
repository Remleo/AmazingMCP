using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using AmazingMCP.Models;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetTypeDepsAndUsageTool
{
    [McpServerTool(Name = "get_type_deps_and_usage", ReadOnly = true), Description(
        "Returns full dependency and usage details for types matching the query. " +
        "Supports exact full name, partial name, and '*' wildcard patterns. " +
        "For each matched abstraction shows: implementations with all dependencies and member usages; " +
        "and which other implementations use this abstraction. Output is Markdown.")]
    public static async Task<string> GetTypeDepsAndUsage(
        DependencyMapService dependencyMapService,
        IDependencyAggregator dependencyAggregator,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the workspace (project root) directory")] string workspacePath,
        [Description("Type query: full name, partial name, or '*' wildcard patterns.")] string typeQuery,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(workspacePath, solutionPath);
        if (resolved is null) return error!;

        var depMap = await dependencyMapService.BuildMapAsync(resolved, ct);
        return FormatMarkdown(depMap, typeQuery, dependencyAggregator);
    }

    internal static string FormatMarkdown(
        DependencyMapResult depMap,
        string typeQuery,
        IDependencyAggregator? aggregator = null)
    {
        if (typeQuery.Contains('*'))
        {
            var wildcardMatches = FindByWildcard(depMap.Abstractions.Keys, typeQuery);
            return wildcardMatches.Count > 0
                ? FormatAbstractionResults(depMap, wildcardMatches, aggregator)
                : $"No types found matching pattern `{typeQuery}`.";
        }

        // Exact match — check abstractions first, then implementations
        if (depMap.Abstractions.ContainsKey(typeQuery))
            return FormatAbstractionResults(depMap, [typeQuery], aggregator);

        if (depMap.Implementations.ContainsKey(typeQuery))
            return FormatImplementationResult(depMap, typeQuery, aggregator);

        return PerformFallbackSearch(depMap, typeQuery, aggregator);
    }

    internal static List<string> FindByWildcard(IEnumerable<string> keys, string pattern)
    {
        var regex = WildcardToRegex(pattern);
        return keys.Where(k => regex.IsMatch(k)).OrderBy(k => k).ToList();
    }

    internal static string PerformFallbackSearch(
        DependencyMapResult depMap, string typeQuery, IDependencyAggregator? aggregator)
    {
        var fuzzyQuery = NormalizeForFuzzySearch(typeQuery);
        var regex = WildcardToRegex(fuzzyQuery);

        var matchedAbstractions = depMap.Abstractions.Keys
            .Where(k => regex.IsMatch(k)).OrderBy(k => k).ToList();
        var matchedImplementations = depMap.Implementations.Keys
            .Where(k => regex.IsMatch(k) && !matchedAbstractions.Contains(k))
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
                {
                    var kind = usage.Kind == MemberUsageKind.MethodCall ? "call" : "prop";
                    var label = usage.Kind == MemberUsageKind.PropertyGet
                        ? $"{usage.MemberName} {{get}}"
                        : usage.Kind == MemberUsageKind.PropertySet
                            ? $"{usage.MemberName} {{set}}"
                            : $"{usage.MemberName}()";
                    sb.AppendLine($"  --- [{kind}] {label}");
                }
            }
            sb.AppendLine();
        }

        // Used by
        var usedBy = depMap.Implementations.Values
            .Where(i => i.Dependencies.Any(d =>
                d.AbstractionFullName == implFullName && d.Usages.Count > 0))
            .ToList();

        if (usedBy.Count > 0)
        {
            sb.AppendLine("## Used by");
            sb.AppendLine();
            var byAbstraction = usedBy
                .SelectMany(i => i.ImplementedAbstractions.Select(a => (Abstraction: a, Impl: i)))
                .GroupBy(x => x.Abstraction)
                .OrderBy(g => g.Key);
            var standalone = usedBy.Where(i => i.ImplementedAbstractions.Count == 0).ToList();

            foreach (var group in byAbstraction)
            {
                sb.AppendLine($"### {group.Key}");
                foreach (var (_, i) in group.OrderBy(x => x.Impl.FullName))
                    AppendImplUsages(sb, i, implFullName);
                sb.AppendLine();
            }
            if (standalone.Count > 0)
            {
                sb.AppendLine("### (standalone)");
                foreach (var i in standalone.OrderBy(x => x.FullName))
                    AppendImplUsages(sb, i, implFullName);
            }
        }

        return sb.ToString().TrimEnd();
    }

    static string FormatAbstractionResults(
        DependencyMapResult depMap,
        List<string> abstractionNames,
        IDependencyAggregator? aggregator)
    {
        var sb = new StringBuilder();

        foreach (var abstractionFullName in abstractionNames)
        {
            if (!depMap.Abstractions.TryGetValue(abstractionFullName, out var abstraction)) continue;

            sb.AppendLine($"# {abstractionFullName}");
            sb.AppendLine();

            if (abstraction.Implementations.Count > 0)
            {
                sb.AppendLine("## Implementations");
                sb.AppendLine();

                foreach (var implName in abstraction.Implementations)
                {
                    sb.AppendLine($"### {implName}");

                    if (!depMap.Implementations.TryGetValue(implName, out var impl))
                    {
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
                        {
                            var kind = usage.Kind == MemberUsageKind.MethodCall ? "call" : "prop";
                            var label = usage.Kind == MemberUsageKind.PropertyGet
                                ? $"{usage.MemberName} {{get}}"
                                : usage.Kind == MemberUsageKind.PropertySet
                                    ? $"{usage.MemberName} {{set}}"
                                    : $"{usage.MemberName}()";
                            sb.AppendLine($"  --- [{kind}] {label}");
                        }
                    }

                    sb.AppendLine();
                }
            }

            // Used by section
            var usedBy = depMap.Implementations.Values
                .Where(impl =>
                {
                    var dep = impl.Dependencies.FirstOrDefault(d => d.AbstractionFullName == abstractionFullName);
                    return dep is not null && dep.Usages.Count > 0;
                })
                .ToList();

            if (usedBy.Count > 0)
            {
                sb.AppendLine("## Used by");
                sb.AppendLine();

                var byAbstraction = usedBy
                    .SelectMany(impl => impl.ImplementedAbstractions
                        .Select(a => (Abstraction: a, Impl: impl)))
                    .GroupBy(x => x.Abstraction)
                    .OrderBy(g => g.Key);

                var standalone = usedBy
                    .Where(impl => impl.ImplementedAbstractions.Count == 0)
                    .ToList();

                foreach (var group in byAbstraction)
                {
                    sb.AppendLine($"### {group.Key}");
                    foreach (var (_, impl) in group.OrderBy(x => x.Impl.FullName))
                        AppendImplUsages(sb, impl, abstractionFullName);
                    sb.AppendLine();
                }

                if (standalone.Count > 0)
                {
                    sb.AppendLine("### (standalone)");
                    foreach (var impl in standalone.OrderBy(i => i.FullName))
                        AppendImplUsages(sb, impl, abstractionFullName);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    static void AppendImplUsages(StringBuilder sb, ImplementationInfo impl, string abstractionFullName)
    {
        sb.AppendLine($"- {impl.FullName}");
        var dep = impl.Dependencies.FirstOrDefault(d => d.AbstractionFullName == abstractionFullName);
        if (dep is null) return;
        foreach (var usage in dep.Usages)
        {
            var kind = usage.Kind == MemberUsageKind.MethodCall ? "call" : "prop";
            var label = usage.Kind == MemberUsageKind.PropertyGet
                ? $"{usage.MemberName} {{get}}"
                : usage.Kind == MemberUsageKind.PropertySet
                    ? $"{usage.MemberName} {{set}}"
                    : $"{usage.MemberName}()";
            sb.AppendLine($"  --- [{kind}] {label}");
        }
    }

    internal static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\<", "<")
            .Replace(@"\>", ">");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }
}
