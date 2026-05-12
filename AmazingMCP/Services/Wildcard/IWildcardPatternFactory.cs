namespace AmazingMCP.Services.Wildcard;

/// <summary>
/// Factory for creating compiled wildcard patterns.
/// </summary>
public interface IWildcardPatternFactory
{
    /// <summary>
    /// Creates a pattern for type name matching (segment-aware).
    /// Leading/trailing '*' matches any sequence; middle '*' stops at ',', ' ', '&lt;', '&gt;'.
    /// </summary>
    IWildcardPattern CreateForTypeNames(string pattern);

    /// <summary>
    /// Creates a pattern for free-text matching (e.g. method signatures).
    /// '*' matches any sequence including all delimiters.
    /// </summary>
    IWildcardPattern CreateGlob(string pattern);
}
