using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetSymbolInfoTool
{
    [McpServerTool(Name = "get_symbol_info", ReadOnly = true), Description(
        "IMPORTANT: THIS TOOL RESOLVES TYPE DETAILS FROM THIRD-PARTY NUGET PACKAGES — " +
        "USE THIS MCP WHEN YOU NEED MEMBERS/PROPERTIES/METHODS OF TYPES FROM EXTERNAL LIBRARIES. " +
        "Returns detailed information about a type by its full name. " +
        "For classes/interfaces: properties, methods (instance and static), constants, fields (instance and static), " +
        "base types, implemented interfaces (recursively), and nested public/internal types. " +
        "For enums: all values. Supports nested type names (e.g. 'Outer.Inner'). " +
        "Use query_symbol first to find the full type name.")]
    public static async Task<string> GetSymbolInfo(
        SymbolInfoService symbolInfo,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description("Fully qualified type name, e.g. 'Bwin.Sports.ContentDistribution.BetContentModelV2.Sport'")] string fullTypeName,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null)
            return error!;

        return await symbolInfo.GetSymbolInfoAsync(resolved, fullTypeName, ct);
    }
}
