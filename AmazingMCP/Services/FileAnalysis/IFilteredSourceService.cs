namespace AmazingMCP.Services.FileAnalysis;

/// <summary>
/// Returns filtered source code of a C# file, showing only members
/// that match the supplied wildcard patterns.
/// </summary>
public interface IFilteredSourceService
{
    /// <summary>
    /// Returns the source of <paramref name="filePath"/> filtered to members
    /// matching <paramref name="filters"/>. When no filters are supplied the
    /// full file is returned (up to a size limit).
    /// </summary>
    string GetFilteredSource(string filePath, string[]? filters);
}
