using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class ReadCsFileDigestTool
{
    [McpServerTool(Name = "read_cs_file_digest", ReadOnly = true), Description(
        "IMPORTANT: USE THIS FIRST when working with large .cs files. " +
        "Returns a compact digest of a .cs file: all namespaces, types, and members with line numbers — " +
        "without implementations. Lets you see the full shape of a large file in one call. " +
        "Then use read_large_cs_file to read the actual source of specific members.")]
    public static string ReadCsFileDigest(
        IFileDigestService fileDigest,
        [Description("Absolute path to the .cs file")] string filePath)
    {
        var result = fileDigest.GetStructure(filePath);
        return result + "\n\n" +
               "> PREFER `read_large_cs_file` over reading the raw file — shows real source of any member by name/signature without loading the whole file.\n" +
               "> Examples: `[\"*ProcessAsync*\"]`, `[\"usings\", \"*public*\"]`, `[\"*Async*\"]`";
    }
}
