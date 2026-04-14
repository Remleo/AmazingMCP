using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class GetFileStructureTool
{
    [McpServerTool(Name = "get_file_structure", ReadOnly = true), Description(
        "Returns the structural outline of a C# source file: namespaces, types (class/interface/struct/record/enum), " +
        "and all their members (fields, constants, properties, constructors, methods, events, operators, nested types) " +
        "with attributes — in source order, without implementations. " +
        "Each entry includes line number, line count (+N lines), and column so the agent can read " +
        "any specific member directly with readFile(path, line, limit).")]
    public static string GetFileStructure(
        FileStructureService fileStructure,
        [Description("Absolute path to the .cs file")] string filePath)
    {
        return fileStructure.GetStructure(filePath);
    }
}
