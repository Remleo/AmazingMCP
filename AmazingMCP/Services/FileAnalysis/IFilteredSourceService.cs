namespace AmazingMCP.Services.FileAnalysis;

/// <summary>
/// Returns filtered source code of a C# file, showing only members
/// that match the supplied wildcard patterns.
/// </summary>
public interface IFilteredSourceService
{
    /// <summary>
    /// Returns the source filtered to members matching <paramref name="filters"/>.
    /// When no filters are supplied the full source is returned.
    /// </summary>
    string GetFilteredSource(string source, string[]? filters);
}
