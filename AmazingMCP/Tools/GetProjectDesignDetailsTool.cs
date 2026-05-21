using System.ComponentModel;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class GetProjectDesignDetailsTool(
    IProjectDesignDetailsService designDetailsService,
    ISolutionResolver solutionResolver)
{
    [McpServerTool(Name = "get_project_design_details", ReadOnly = true), Description(
        "Deep-dives into the design of specific namespace groups to help understand how the project is structured. " +
        "Call after `get_project_design` to explore areas of interest in detail. " +
        "Supports `*` wildcard in namespace patterns. " +
        "Use `includeDependencyUsage: false` or `includeImplementations: false` to reduce output size.")]
    public async Task<string> GetDetailedProjectDesign(
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

        return await designDetailsService.GetDetailsAsync(resolved, forNamespaces, includeDependencyUsage, includeImplementations, ct);
    }
}
