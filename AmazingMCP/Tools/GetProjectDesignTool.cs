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
        "The best starting point for understanding how a project is designed. " +
        "Gives a helicopter view of the entire codebase — its key building blocks, how they are organized, and how they relate to each other. " +
        "Highly recommended before making any non-trivial changes or exploring an unfamiliar codebase. " +
        "Follow up with `get_project_design_details` to dive deeper into specific areas.")]
    public static async Task<string> GetProjectDesign(
        ProjectDesignService projectDesignService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
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
        sb.AppendLine("> To get detailed info for specific groups, call `get_project_design_details` with `forNamespaces`.");
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
                    sb.AppendLine($"- {dep}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
