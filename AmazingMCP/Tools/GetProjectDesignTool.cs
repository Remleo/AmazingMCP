using System.ComponentModel;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class GetProjectDesignTool(
    IProjectDesignService projectDesignService,
    ISolutionResolver solutionResolver)
{
    [McpServerTool(Name = "get_project_design", ReadOnly = true), Description(
        "The best starting point for understanding how a project is designed. " +
        "Gives a helicopter view of the entire codebase — its key building blocks, how they are organized, and how they relate to each other. " +
        "Highly recommended before making any non-trivial changes or exploring an unfamiliar codebase. " +
        "Follow up with `get_project_design_details` to dive deeper into specific areas.")]
    public async Task<string> GetProjectDesign(
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null) return error!;

        return await projectDesignService.GetDesignAsync(resolved, ct);
    }
}
