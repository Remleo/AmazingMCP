using System.ComponentModel;
using AmazingMCP.Services;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetTypeDetailsTool
{
    [McpServerTool(Name = "get_type_details", ReadOnly = true), Description(
        "IMPORTANT: THIS TOOL RESOLVES TYPE DETAILS FROM THIRD-PARTY NUGET PACKAGES — " +
        "USE THIS MCP WHEN YOU NEED MEMBERS/PROPERTIES/METHODS OF TYPES FROM EXTERNAL LIBRARIES. " +
        "Returns detailed information about a type by its full name. " +
        "For classes/interfaces: properties, methods (instance and static), constants, fields (instance and static), " +
        "base types, implemented interfaces (recursively), nested public/internal types, " +
        "and known implementors / derived types. " +
        "For enums: all values. Supports nested type names (e.g. 'Outer.Inner'). " +
        "Use query_symbol first to find the full type name.")]
    public static async Task<string> GetTypeDetails(
        SymbolInfoService symbolInfo,
        ISolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description("Fully qualified type name. Supports C# generic syntax (e.g. 'System.Collections.Generic.List<T>') and CLR metadata notation (e.g. 'System.Collections.Generic.List`1')")] string fullTypeName,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        [Description("Optional wildcard filters to show only matching members (e.g. [\"*Get*\", \"Create*\", \"MemberFullName\"]).")] string[] memberFilters = null!,
        [Description("Optional NuGet version to show (e.g. '12.0.1'). When omitted, the highest available version is shown.")] string? version = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null)
            return error!;

        return await symbolInfo.GetTypeDetailsAsync(resolved, fullTypeName, memberFilters, version, ct);
    }
}
