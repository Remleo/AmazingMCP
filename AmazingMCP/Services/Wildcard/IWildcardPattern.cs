namespace AmazingMCP.Services.Wildcard;

/// <summary>
/// A compiled wildcard pattern that can match strings.
/// Create via <see cref="IWildcardPatternFactory"/>.
/// </summary>
public interface IWildcardPattern
{
    bool IsMatch(string input);
}
