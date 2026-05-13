using System.ComponentModel;
using AmazingMCP.Services.FileAnalysis;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class ReadCsFileDigestTool(IReadCsFileDigestService digestService)
{
    [McpServerTool(Name = "read_cs_file_digest", ReadOnly = true), Description(
        "IMPORTANT: USE THIS FIRST when working with large .cs files. " +
        "Returns a compact digest of a .cs file: all namespaces, types, and members with line numbers — " +
        "without implementations. Lets you see the full shape of a large file in one call. " +
        "Then use read_large_cs_file to read the actual source of specific members.")]
    public string ReadCsFileDigest(
        [Description("Absolute path to the .cs file")] string filePath)
        => digestService.Read(filePath);
}
