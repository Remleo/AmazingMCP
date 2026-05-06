using System.Text.RegularExpressions;

namespace AmazingMCP.Services;

/// <summary>
/// A compiled wildcard pattern. Create via <see cref="IWildcardPatternFactory"/>.
/// </summary>
public sealed class WildcardPattern(Regex regex) : IWildcardPattern
{
    public bool IsMatch(string input) => regex.IsMatch(input);
}
