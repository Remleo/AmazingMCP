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
    const int MaxOutputLength = 10000;
    const string TruncationSuffix =
        "\n\n<<... truncated output ...>> Please try to use more specific namespaces, `includeDependencyUsage: false` or `includeImplementations: false`";

    [McpServerTool(Name = "get_detailed_project_design", ReadOnly = true), Description(
        "Returns a detailed view of abstractions and their implementations for the specified namespace groups. " +
        "An abstraction is an interface or standalone class that can be injected via DI. " +
        "Each abstraction entry shows its implementations, their constructor dependencies, and (optionally) which members are called on each dependency. " +
        "To drill into a single type in full detail, use `get_type_deps_and_usage`. " +
        "Use `get_project_design` first to discover available groups and their namespaces, " +
        "then pass those namespaces here. Output is Markdown.")]
    public static async Task<string> GetDetailedProjectDesign(
        DependencyMapService dependencyMapService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the workspace (project root) directory")] string workspacePath,
        [Description(
            "Namespaces to include. Each entry is matched against abstraction namespaces. " +
            "Supports exact match (e.g. 'MyApp.Services') and '*' wildcard anywhere " +
            "(e.g. 'MyApp.*', '*.Services', 'MyApp.*.Handlers'). " +
            "At least one entry is required.")]
        string[] forNamespaces,
        [Description("When true (default), shows which methods and properties are called on each dependency.")]
        bool includeDependencyUsage = true,
        [Description("When true (default), shows the list of implementations for each abstraction. Set to false to show only dependencies — useful for large namespaces.")]
        bool includeImplementations = true,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        if (forNamespaces is null || forNamespaces.Length == 0)
            return "Error: `forNamespaces` must contain at least one namespace pattern.";

        var (resolved, error) = solutionResolver.Resolve(workspacePath, solutionPath);
        if (resolved is null)
            return error!;

        var depMap = await dependencyMapService.BuildMapAsync(resolved, ct);
        return FormatMarkdown(depMap, forNamespaces, includeDependencyUsage, includeImplementations);
    }

    internal static string FormatMarkdown(
        DependencyMapResult depMap,
        string[] forNamespaces,
        bool includeDependencyUsage,
        bool includeImplementations = true)
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

            if (includeDependencyUsage || abstraction.Implementations.Any(i => depMap.Implementations.TryGetValue(i, out var im) && im.Dependencies.Count > 0))
            {
                sb.AppendLine();
                sb.AppendLine("### Depends on");

                // Collect unique deps across all implementations
                var shownDeps = new HashSet<string>();
                foreach (var implName in abstraction.Implementations)
                {
                    if (!depMap.Implementations.TryGetValue(implName, out var impl))
                        continue;

                    foreach (var dep in impl.Dependencies)
                    {
                        var depLabel = dep.IsOptions ? $"IOptions<{dep.TypeFullName}>"
                            : dep.IsEnumerable ? $"IEnumerable<{dep.TypeFullName}>"
                            : dep.TypeFullName;

                        if (!shownDeps.Add(depLabel)) continue;

                        sb.AppendLine($"- {depLabel}");

                        if (includeDependencyUsage &&
                            impl.DependencyMemberUsages.TryGetValue(dep.TypeFullName, out var usages))
                        {
                            foreach (var usage in usages)
                            {
                                var kind = usage.Kind == MemberUsageKind.MethodCall ? "call" : "prop";
                                var label = usage.Kind == MemberUsageKind.PropertyGet ? $"{usage.MemberName} {{get}}"
                                    : usage.Kind == MemberUsageKind.PropertySet ? $"{usage.MemberName} {{set}}"
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
