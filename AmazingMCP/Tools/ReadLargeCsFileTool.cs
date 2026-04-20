using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class ReadLargeCsFileTool
{
    [McpServerTool(Name = "read_large_cs_file", ReadOnly = true), Description(
        "IMPORTANT: USE THIS to read source code from large .cs files instead of loading the full file. " +
        "Returns only the members matching the given wildcard filters, with skipped sections replaced by '// << ... cut ... >>'. " +
        "Filters match against full member signatures (name, return type, parameters). " +
        "If unsure what members exist, call read_cs_file_digest first. ")]
    public static string ReadLargeCsFile(
        FilteredSourceService filteredSource,
        [Description("Absolute path to the .cs file")] string filePath,
        [Description("Wildcard filter patterns, e.g. [\"*Async*\", \"usings\", \"*public*\"]. Pass null or empty to return the full file.")]
        string[]? filters = null)
    {
        var result = filteredSource.GetFilteredSource(filePath, filters);
        if (result.Contains("No matches found"))
            return result + "\n\n" +
                   "> No members matched. Use `read_cs_file_digest` to see the compact outline and find correct member names/signatures.";
        return result + "\n\n" +
               "> Use `read_cs_file_digest` to see the full compact outline of this file.";
    }
}
