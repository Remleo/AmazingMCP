using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class ReadLargeCsharpFileTool
{
    [McpServerTool(Name = "read_large_csharp_file", ReadOnly = true), Description(
        "IMPORTANT: USE THIS to read source code from large C# files instead of loading the full file. " +
        "Returns only the members matching the given wildcard filters, with skipped sections replaced by '// << ... cut ... >>'. " +
        "Filters match against full member signatures (name, return type, parameters). " +
        "If unsure what members exist, call read_csharp_file_digest first.")]
    public static string ReadLargeCsharpFile(
        FilteredSourceService filteredSource,
        [Description("Absolute path to the .cs file")] string filePath,
        [Description("Wildcard filter patterns, e.g. [\"*Async*\", \"usings\", \"*public*\"]")]
        string[] filters)
    {
        return filteredSource.GetFilteredSource(filePath, filters);
    }
}
