using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetFilteredSourceTool
{
    [McpServerTool(Name = "get_filtered_source", ReadOnly = true), Description(
        "Returns the source file with only the members/types that match the given wildcard filters. " +
        "Each filter is a string that may contain '*' as a wildcard (e.g. '*Handler*', 'public * Get*'). " +
        "Matching is case-insensitive and applied to the member signature. " +
        "Skipped sections are replaced with '// << ... cut ... >>'. " +
        "Special filter 'usings' includes the using-directives block. " +
        "Types with more than 200 lines are never matched as a whole (only their individual members are). " +
        "Useful for reading a large file focused on specific members without loading the entire source.")]
    public static string GetFilteredSource(
        FilteredSourceService filteredSource,
        [Description("Absolute path to the .cs file")] string filePath,
        [Description("Wildcard filter patterns, e.g. [\"*Handler*\", \"usings\", \"public class *\"]")]
        string[] filters)
    {
        return filteredSource.GetFilteredSource(filePath, filters);
    }
}
