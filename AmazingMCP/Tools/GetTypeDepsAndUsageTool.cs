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
        "Supports exact full name, partial name, and '*' wildcard patterns (e.g. '*.IMyService', 'MyApp.Services.*'). " +
        "For each matched abstraction shows: (1) its implementations with all constructor dependencies and member usages; " +
        "(2) which other implementations use this abstraction as a dependency, grouped by their abstraction, " +
        "showing only usages of the queried abstraction. " +
        "If no exact match is found and the query has no wildcards, performs a fuzzy search " +
        "across both abstractions and implementations. Output is Markdown.")]
    public static async Task<string> GetTypeDepsAndUsage(
        DependencyMapService dependencyMapService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the workspace (project root) directory")] string workspacePath,
        [Description(
            "Type query to search for. Supports: full name (e.g. 'MyApp.Services.IMyService'), " +
            "partial name, or '*' wildcard patterns (e.g. '*.IMyService', 'MyApp.*.Handler'). " +
            "When no exact match is found and no wildcards are present, a fuzzy search is performed automatically.")]
        string typeQuery,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(workspacePath, solutionPath);
        if (resolved is null)
            return error!;

        var depMap = await dependencyMapService.BuildMapAsync(resolved, ct);
        return FormatMarkdown(depMap, typeQuery);
    }

    internal static string FormatMarkdown(DependencyMapResult depMap, string typeQuery)
    {
        // Step 1: Try exact match in abstractions
        if (!typeQuery.Contains('*') && depMap.Abstractions.ContainsKey(typeQuery))
            return FormatAbstractionResults(depMap, [typeQuery]);

        // Step 2: If query contains wildcards, do wildcard search only
        if (typeQuery.Contains('*'))
        {
            var wildcardMatches = FindByWildcard(depMap.Abstractions.Keys, typeQuery);
            if (wildcardMatches.Count > 0)
                return FormatAbstractionResults(depMap, wildcardMatches);

            return $"No types found matching pattern `{typeQuery}`.";
        }

        // Step 3: No wildcards, no exact match — fallback fuzzy search
        return PerformFallbackSearch(depMap, typeQuery);
    }

    internal static List<string> FindByWildcard(IEnumerable<string> keys, string pattern)
    {
        var regex = WildcardToRegex(pattern);
        return keys.Where(k => regex.IsMatch(k)).OrderBy(k => k).ToList();
    }

    internal static string PerformFallbackSearch(DependencyMapResult depMap, string typeQuery)
    {
        var fuzzyQuery = NormalizeForFuzzySearch(typeQuery);
        var regex = WildcardToRegex(fuzzyQuery);

        var matchedAbstractions = depMap.Abstractions.Keys
            .Where(k => regex.IsMatch(k))
            .OrderBy(k => k)
            .ToList();

        var matchedImplementations = depMap.Implementations.Keys
            .Where(k => regex.IsMatch(k) && !matchedAbstractions.Contains(k))
            .OrderBy(k => k)
            .ToList();

        if (matchedAbstractions.Count == 0 && matchedImplementations.Count == 0)
            return $"No exact match found for `{typeQuery}`. " +
                   $"Fuzzy search with pattern `{fuzzyQuery}` also returned no results.";

        var sb = new StringBuilder();
        sb.AppendLine($"No exact match found for `{typeQuery}`.");
        sb.AppendLine($"Showing results for fuzzy search pattern `{fuzzyQuery}`:");
        sb.AppendLine();

        if (matchedAbstractions.Count > 0)
            sb.Append(FormatAbstractionResults(depMap, matchedAbstractions));

        if (matchedImplementations.Count > 0)
        {
            if (matchedAbstractions.Count > 0)
                sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Matched implementations");
            sb.AppendLine();

            foreach (var implName in matchedImplementations)
            {
                if (!depMap.Implementations.TryGetValue(implName, out var impl))
                    continue;

                sb.AppendLine($"### {implName}");

                if (impl.ImplementedAbstractions.Count > 0)
                {
                    sb.AppendLine("Implements:");
                    foreach (var abs in impl.ImplementedAbstractions)
                        sb.AppendLine($"- {abs}");
                }

                if (impl.Dependencies.Count > 0)
                {
                    sb.AppendLine("Depends on:");
                    foreach (var dep in impl.Dependencies)
                    {
                        var depLabel = dep.IsOptions ? $"IOptions<{dep.TypeFullName}>"
                            : dep.IsEnumerable ? $"IEnumerable<{dep.TypeFullName}>"
                            : dep.TypeFullName;
                        sb.AppendLine($"- {depLabel}");
                    }
                }

                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    internal static string NormalizeForFuzzySearch(string query)
    {
        var normalized = query;

        // If generic (contains < >), replace generic params with wildcards
        if (normalized.Contains('<') && normalized.Contains('>'))
        {
            var openIdx = normalized.IndexOf('<');
            var closeIdx = normalized.LastIndexOf('>');
            if (closeIdx > openIdx)
            {
                var genericPart = normalized.Substring(openIdx + 1, closeIdx - openIdx - 1);
                var commaCount = genericPart.Count(c => c == ',');
                // Each empty slot becomes * to match any type param
                var wildcardParams = string.Join(", ", Enumerable.Repeat("*", commaCount + 1));
                normalized = normalized[..openIdx] + "<" + wildcardParams + ">";
            }
        }

        // Wrap with * at start and end
        if (!normalized.StartsWith("*"))
            normalized = "*" + normalized;
        if (!normalized.EndsWith("*"))
            normalized = normalized + "*";

        return normalized;
    }

    static string FormatAbstractionResults(DependencyMapResult depMap, List<string> abstractionNames)
    {
        var sb = new StringBuilder();

        foreach (var abstractionFullName in abstractionNames)
        {
            if (!depMap.Abstractions.TryGetValue(abstractionFullName, out var abstraction))
                continue;

            sb.AppendLine($"# {abstractionFullName}");
            sb.AppendLine();

            if (abstraction.Implementations.Count > 0)
            {
                sb.AppendLine("## Implementations");
                sb.AppendLine();

                foreach (var implName in abstraction.Implementations)
                {
                    sb.AppendLine($"### {implName}");

                    if (!depMap.Implementations.TryGetValue(implName, out var impl) ||
                        impl.Dependencies.Count == 0)
                    {
                        sb.AppendLine();
                        continue;
                    }

                    sb.AppendLine("Depends on:");
                    foreach (var dep in impl.Dependencies)
                    {
                        var depLabel = dep.IsOptions ? $"IOptions<{dep.TypeFullName}>"
                            : dep.IsEnumerable ? $"IEnumerable<{dep.TypeFullName}>"
                            : dep.TypeFullName;

                        sb.AppendLine($"- {depLabel}");

                        if (impl.DependencyMemberUsages.TryGetValue(dep.TypeFullName, out var usages))
                        {
                            foreach (var usage in usages)
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
                    }

                    sb.AppendLine();
                }
            }

            var usedBy = depMap.Implementations.Values
                .Where(impl =>
                    impl.Dependencies.Any(d => d.TypeFullName == abstractionFullName) &&
                    impl.DependencyMemberUsages.ContainsKey(abstractionFullName))
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
                    {
                        sb.AppendLine($"- {impl.FullName}");
                        if (impl.DependencyMemberUsages.TryGetValue(abstractionFullName, out var usages))
                        {
                            foreach (var usage in usages)
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
                    }

                    sb.AppendLine();
                }

                if (standalone.Count > 0)
                {
                    sb.AppendLine("### (standalone)");
                    foreach (var impl in standalone.OrderBy(i => i.FullName))
                    {
                        sb.AppendLine($"- {impl.FullName}");
                        if (impl.DependencyMemberUsages.TryGetValue(abstractionFullName, out var usages))
                        {
                            foreach (var usage in usages)
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
                    }
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    internal static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\<", "<")
            .Replace(@"\>", ">");
        // In regex, < and > are literal characters (not special), safe to unescape
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }
}
