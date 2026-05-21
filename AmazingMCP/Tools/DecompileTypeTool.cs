using System.ComponentModel;
using AmazingMCP.Services.Decompile;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class DecompileTypeTool
{
    [McpServerTool(Name = "decompile_type", ReadOnly = true), Description(
        "Decompiles a type from a NuGet assembly and returns its C# source code. " +
        "Use this when you need to see the actual implementation of a third-party type. " +
        "For types defined in solution source files, use read_cs_file_digest instead. " +
        "Optionally filter to specific members using wildcard patterns (e.g. [\"*Get*\", \"Create*\"]). " +
        "Constructors are always included when memberFilters are specified. " +
        "Use query_symbol first to find the full type name.")]
    public static async Task<string> DecompileTypeAsync(
        IDecompileTypeService decompileTypeService,
        ISolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")]
        string solutionWorkspacePath,
        [Description("Fully qualified type name (e.g. 'AutoMapper.MapperConfiguration')")]
        string fullTypeName,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")]
        string? solutionPath = null,
        [Description("Optional wildcard filters to show only matching members (e.g. [\"*Get*\", \"Create*\"]).")]
        string[]? memberFilters = null,
        CancellationToken ct = default)
    {
        var (resolvedPath, resolveError) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolvedPath is null)
            return resolveError!;
        return await decompileTypeService.DecompileTypeAsync(resolvedPath, fullTypeName, memberFilters, ct);
    }
}
