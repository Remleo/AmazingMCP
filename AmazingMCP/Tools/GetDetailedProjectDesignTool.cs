using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using AmazingMCP.Models;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetDetailedProjectDesignTool
{
    const int MaxOutputLength = 30000;
    const string TruncationSuffix =
        "\n\n<<... truncated output ...>> Please try to use more specific namespaces, `includeDependencyUsage: false` or `includeImplementations: false`";

    [McpServerTool(Name = "get_detailed_project_design", ReadOnly = true), Description(
        "Returns a detailed view of abstractions and their implementations for the specified namespace groups. " +
        "Each abstraction entry shows its implementations, their dependencies, and (optionally) which members are called on each dependency. " +
        "Use `get_project_design` first to discover available groups and their namespaces. Output is Markdown.")]
    public static async Task<string> GetDetailedProjectDesign(
        DependencyMapService dependencyMapService,
        IDependencyAggregator dependencyAggregator,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the workspace (project root) directory")] string workspacePath,
        [Description(
            "Namespaces to include. Supports exact match and '*' wildcard anywhere. At least one entry is required.")]
        string[] forNamespaces,
        [Description("When true (default), shows which methods and properties are called on each dependency.")]
        bool includeDependencyUsage = true,
        [Description("When true (default), shows the list of implementations for each abstraction.")]
        bool includeImplementations = true,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        if (forNamespaces is null || forNamespaces.Length == 0)
            return "Error: `forNamespaces` must contain at least one namespace pattern.";

        var (resolved, error) = solutionResolver.Resolve(workspacePath, solutionPath);
        if (resolved is null) return error!;

        var depMap = await dependencyMapService.BuildMapAsync(resolved, ct);
        return FormatMarkdown(depMap, forNamespaces, includeDependencyUsage, includeImplementations, dependencyAggregator);
    }

    internal static string FormatMarkdown(
        DependencyMapResult depMap,
        string[] forNamespaces,
        bool includeDependencyUsage,
        bool includeImplementations = true,
        IDependencyAggregator? aggregator = null)
    {
        var patterns = forNamespaces.Select(WildcardToRegex).ToList();

        var matchedAbstractions = depMap.Abstractions.Values
            .Where(a => a.SourceFilePath is not null)
            .Where(a => patterns.Any(p => p.IsMatch(a.Namespace)))
            .OrderBy(a => a.Namespace)
            .ThenBy(a => a.FullName)
            .ToList();

        if (matchedAbstractions.Count == 0)
            return $"No abstractions found matching the provided namespace pattern(s): {string.Join(", ", forNamespaces)}";

        var sb = new StringBuilder();
        sb.AppendLine("# Detailed Project Design");
        sb.AppendLine();

        foreach (var abstraction in matchedAbstractions)
        {
            sb.AppendLine($"## {abstraction.FullName}");

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
                sb.AppendLine();
                sb.AppendLine("### Depends on");

                var shownDeps = new HashSet<string>();
                foreach (var implName in abstraction.Implementations)
                {
                    if (!depMap.Implementations.TryGetValue(implName, out _)) continue;

                    var allUsages = aggregator is not null
                        ? aggregator.GetAllUsages(implName, depMap)
                        : depMap.Implementations[implName].Dependencies;

                    foreach (var dep in allUsages)
                    {
                        if (!shownDeps.Add(dep.AbstractionFullName)) continue;

                        sb.AppendLine($"- {dep.AbstractionFullName}");

                        if (includeDependencyUsage && dep.Usages.Count > 0)
                        {
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

    internal static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace(@"\*", ".*");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }
}
