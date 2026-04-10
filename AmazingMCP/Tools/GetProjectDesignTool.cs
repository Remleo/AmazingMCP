using System.ComponentModel;
using System.Text;
using AmazingMCP.Models;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetProjectDesignTool
{
    [McpServerTool(Name = "get_project_design", ReadOnly = true), Description(
        "Returns a high-level project design showing abstraction groups and their " +
        "inter-group dependencies. Groups are formed by namespace hierarchy. " +
        "For each group, shows the abstraction count and which other groups it depends on " +
        "(resolved from constructor injection across all implementations and base class chains). " +
        "Output is Markdown.")]
    public static async Task<string> GetProjectDesign(
        ProjectDesignService projectDesignService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the workspace (project root) directory")] string workspacePath,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(workspacePath, solutionPath);
        if (resolved is null)
            return error!;

        var design = await projectDesignService.BuildAsync(resolved, ct);
        return FormatMarkdown(design);
    }

    internal static string FormatMarkdown(ProjectDesignResult design)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Project Design");
        sb.AppendLine();
        sb.AppendLine("> Each group is shown as: `## ShortName (FullNamespace)`");
        sb.AppendLine("> To get detailed info for specific groups, call `get_detailed_project_design` with `forNamespaces`.");
        sb.AppendLine("> Use the `FullNamespace` value directly, or use `*` as a wildcard anywhere (e.g. `MyApp.App.*`, `*.Mapping`, `MyApp.*.Services`).");
        sb.AppendLine();

        foreach (var group in design.Groups)
        {
            var label = string.IsNullOrEmpty(group.Name) ? "(root)" : group.Name;

            sb.AppendLine($"## {label} ({group.FullName})");
            sb.AppendLine($"Entries count: {group.EntryCount}");

            if (group.DependsOn.Count > 0)
            {
                sb.AppendLine("Depends on:");
                foreach (var dep in group.DependsOn)
                    sb.AppendLine($"- → {dep}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
