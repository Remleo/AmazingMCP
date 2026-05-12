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

[McpServerToolType]
public static class GetProjectDesignDetailsTool
{
    const int MaxOutputLength = 30000;
    const int MaxSummaryLength = 2000;
    const string TruncationSuffix =
        "\n\n<<... truncated output ...>> Please try to use more specific namespaces, `includeDependencyUsage: false` or `includeImplementations: false`";

    [McpServerTool(Name = "get_project_design_details", ReadOnly = true), Description(
        "Deep-dives into the design of specific namespace groups to help understand how the project is structured. " +
        "Call after `get_project_design` to explore areas of interest in detail. " +
        "Supports `*` wildcard in namespace patterns. " +
        "Use `includeDependencyUsage: false` or `includeImplementations: false` to reduce output size.")]
    public static async Task<string> GetDetailedProjectDesign(
        IDependencyMapService dependencyMapService,
        IDependencyAggregator dependencyAggregator,
        SolutionResolver solutionResolver,
        IWildcardPatternFactory wildcardFactory,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description(
            "Namespaces to include. Supports exact match and '*' wildcard anywhere. At least one entry is required.")]
        string[] forNamespaces,
        [Description("When false (default), shows which methods and properties are called on each dependency.")]
        bool includeDependencyUsage = false,
        [Description("When true (default), shows the list of implementations for each abstraction.")]
        bool includeImplementations = true,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        if (forNamespaces is null || forNamespaces.Length == 0)
            return "Error: `forNamespaces` must contain at least one namespace pattern.";

        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null) return error!;

        var depMap = await dependencyMapService.BuildMapAsync(resolved, ct);
        return FormatMarkdown(depMap, forNamespaces, includeDependencyUsage, includeImplementations, wildcardFactory, dependencyAggregator);
    }

    internal static string FormatMarkdown(
        DependencyMapResult depMap,
        string[] forNamespaces,
        bool includeDependencyUsage,
        bool includeImplementations = true,
        IWildcardPatternFactory? wildcardFactory = null,
        IDependencyAggregator? aggregator = null)
    {
        var factory = wildcardFactory ?? new WildcardPatternFactory();
        var patterns = forNamespaces.Select(factory.CreateGlob).ToList();

        var matchedAbstractions = depMap.Abstractions.Values
            .Where(a => a.SourceFilePath is not null)
            .Where(a => patterns.Any(p => p.IsMatch(a.Namespace)))
            .OrderBy(a => a.Namespace)
            .ThenBy(a => a.FullName)
            .ToList();

        if (matchedAbstractions.Count == 0)
            return $"No abstractions found matching the provided namespace pattern(s): {string.Join(", ", forNamespaces)}";

        // Build collapse structures for dependency names in output
        var openToClosedIndex = GenericCollapseHelper.BuildOpenToClosedIndex(depMap.ClosedToOpenGenericMap);

        var sb = new StringBuilder();
        sb.AppendLine("# Project Design Details");
        sb.AppendLine();
        sb.AppendLine($"> Namespaces: `{string.Join("`, `", forNamespaces)}`");
        sb.AppendLine($"> Abstractions found: {matchedAbstractions.Count}");
        sb.AppendLine();

        foreach (var abstraction in matchedAbstractions)
        {
            sb.AppendLine($"## {abstraction.FullName}");

            if (!string.IsNullOrEmpty(abstraction.XmlDocSummary))
            {
                var summary = abstraction.XmlDocSummary.Length > MaxSummaryLength
                    ? abstraction.XmlDocSummary[..MaxSummaryLength] + " <<truncated>>"
                    : abstraction.XmlDocSummary;
                sb.AppendLine($"> {summary}");
            }

            if (abstraction.Implementations.Count == 0)
            {
                sb.AppendLine();
                continue;
            }

            if (includeImplementations)
            {
                sb.AppendLine("### Implementations");
                foreach (var implName in abstraction.Implementations)
                    sb.AppendLine($"- {implName}");
            }

            // Collect all deps across implementations using aggregator
            var hasDeps = abstraction.Implementations.Any(i =>
                depMap.Implementations.TryGetValue(i, out var im) && im.Dependencies.Count > 0);

            if (includeDependencyUsage || hasDeps)
            {
                sb.AppendLine("### Depends on");

                // Collect raw dep names first, then collapse closed generics
                var rawDepNames = new List<string>();
                var rawDepUsages = new Dictionary<string, List<MemberUsage>>();

                foreach (var implName in abstraction.Implementations)
                {
                    if (!depMap.Implementations.TryGetValue(implName, out _)) continue;

                    var allUsages = aggregator is not null
                        ? aggregator.GetAllUsages(implName, depMap)
                        : depMap.Implementations[implName].Dependencies;

                    foreach (var dep in allUsages)
                    {
                        if (!rawDepUsages.ContainsKey(dep.AbstractionFullName))
                        {
                            rawDepNames.Add(dep.AbstractionFullName);
                            rawDepUsages[dep.AbstractionFullName] = [];
                        }
                        foreach (var u in dep.Usages)
                            rawDepUsages[dep.AbstractionFullName].Add(u);
                    }
                }

                // Collapse closed generics in the dep list:
                // always collapse if the open generic exists in abstractions
                var (finalDepNames, collapsedDeps) = CollapseDepNames(
                    rawDepNames, depMap.ClosedToOpenGenericMap, openToClosedIndex, depMap.Abstractions);

                foreach (var depName in finalDepNames)
                {
                    // Aggregate usages: from the dep itself + any collapsed closeds
                    var effectiveDepNames = GenericCollapseHelper
                        .GetEffectiveAbstractionNames(depName, collapsedDeps);

                    var aggregatedUsages = effectiveDepNames
                        .SelectMany(n => rawDepUsages.TryGetValue(n, out var u) ? u : [])
                        .DistinctBy(u => (u.MemberName, u.Kind))
                        .ToList();

                    sb.AppendLine($"- {depName}");

                    if (includeDependencyUsage && aggregatedUsages.Count > 0)
                    {
                        foreach (var usage in aggregatedUsages)
                        {
                            var label = usage.Kind == MemberUsageKind.PropertyGet
                                ? $"{usage.MemberName} {{get}}"
                                : usage.Kind == MemberUsageKind.PropertySet
                                    ? $"{usage.MemberName} {{set}}"
                                    : $"{usage.MemberName}()";
                            sb.AppendLine($"  - {label}");
                        }
                    }
                }
            }

            sb.AppendLine();
        }

        var result = sb.ToString().TrimEnd();
        if (result.Length > MaxOutputLength)
            return result[..MaxOutputLength] + TruncationSuffix;

        return result;
    }

    /// <summary>
    /// Collapses closed generic dep names into their open generic when the open generic
    /// exists in abstractions — regardless of whether it's in the current match.
    /// Used for Depends on sections where we always want to show the canonical name.
    /// </summary>
    static (List<string> FinalNames, IReadOnlyDictionary<string, List<string>> CollapsedCloseds)
        CollapseDepNames(
            IReadOnlyList<string> depNames,
            IReadOnlyDictionary<string, string>? closedToOpenMap,
            IReadOnlyDictionary<string, List<string>> openToClosedIndex,
            IReadOnlyDictionary<string, AbstractionInfo> abstractions)
    {
        if (closedToOpenMap is null || closedToOpenMap.Count == 0)
            return (depNames.ToList(), new Dictionary<string, List<string>>());

        var collapsedCloseds = new Dictionary<string, List<string>>();
        var skipped = new HashSet<string>();
        var openGenericsToAdd = new List<string>();

        foreach (var name in depNames)
        {
            if (!closedToOpenMap.TryGetValue(name, out var openName)) continue;
            // Collapse if open generic exists in abstractions
            if (!abstractions.ContainsKey(openName)) continue;

            skipped.Add(name);
            if (!collapsedCloseds.TryGetValue(openName, out var list))
            {
                collapsedCloseds[openName] = list = [];
                // Open generic may not be in depNames — add it
                if (!depNames.Contains(openName))
                    openGenericsToAdd.Add(openName);
            }
            list.Add(name);
        }

        var finalNames = depNames
            .Where(n => !skipped.Contains(n))
            .Concat(openGenericsToAdd)
            .ToList();

        // Also collapse ALL closeds of each open generic (not just those in depNames)
        foreach (var openName in collapsedCloseds.Keys.ToList())
        {
            if (!openToClosedIndex.TryGetValue(openName, out var allCloseds)) continue;
            collapsedCloseds[openName] = allCloseds;
        }

        return (finalNames, collapsedCloseds);
    }
}
