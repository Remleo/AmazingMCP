using System.Text;
using AmazingMCP.Models.Design;

namespace AmazingMCP.Services.Design;

public class ProjectDesignService(
    IProjectDesignProvider projectDesignProvider) : IProjectDesignService
{
    public async Task<string> GetDesignAsync(string solutionPath, CancellationToken ct = default)
    {
        var design = await projectDesignProvider.BuildAsync(solutionPath, ct);
        return Format(design);
    }

    internal static string Format(ProjectDesignResult design)
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
            sb.AppendLine();

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
