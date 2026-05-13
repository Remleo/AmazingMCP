using System.ComponentModel;
using AmazingMCP.Services.FileAnalysis;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class ReadLargeCsFileTool(IReadLargeCsFileService readService)
{
    [McpServerTool(Name = "read_large_cs_file", ReadOnly = true), Description(
        "IMPORTANT: USE THIS to read source code from large .cs files instead of loading the full file. " +
        "Returns only the members matching the given wildcard filters. " +
        "Filters match against full member signatures (name, return type, parameters). " +
        "If unsure what members exist, call read_cs_file_digest first.")]
    public string ReadLargeCsFile(
        [Description("Absolute path to the .cs file")] string filePath,
        [Description("Wildcard filter patterns, e.g. [\"*Async*\", \"usings\", \"*public*\"]. Pass empty array to return the full file.")]
#pragma warning disable CS8625
        string[] filters = null)
#pragma warning restore CS8625
        => readService.Read(filePath, filters);
}
