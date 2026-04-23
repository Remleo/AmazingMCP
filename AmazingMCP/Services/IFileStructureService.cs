using AmazingMCP.Models;

namespace AmazingMCP.Services;

/// <summary>
/// Parses a C# source file and returns its structural outline
/// (namespaces, types, members) with line-number metadata.
/// </summary>
public interface IFileStructureService
{
    /// <summary>
    /// Returns a flat list of structural items (usings, namespaces, types, members)
    /// found in the given C# file.
    /// </summary>
    List<FileStructureItem> GetItems(string filePath);

    /// <summary>
    /// Returns a human-readable, indented digest of the file structure
    /// with line-number annotations.
    /// </summary>
    string GetStructure(string filePath);
}
