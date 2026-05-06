namespace AmazingMCP.Services;

/// <summary>
/// Produces a human-readable, indented digest of a C# file structure
/// with line-number annotations.
/// </summary>
public interface IFileDigestService
{
    string GetStructure(string filePath);
}
