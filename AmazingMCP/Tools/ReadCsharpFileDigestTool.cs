using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class ReadCsharpFileDigestTool
{
    [McpServerTool(Name = "read_csharp_file_digest", ReadOnly = true), Description(
        "IMPORTANT: USE THIS FIRST when working with large C# files. " +
        "Returns a compact digest of a C# file: all namespaces, types, and members with line numbers — " +
        "without implementations. Lets you see the full shape of a large file in one call. " +
        "Then use read_large_csharp_file to read the actual source of specific members.")]
    public static string ReadCsharpFileDigest(
        FileStructureService fileStructure,
        [Description("Absolute path to the .cs file")] string filePath)
    {
        return fileStructure.GetStructure(filePath);
    }
}
