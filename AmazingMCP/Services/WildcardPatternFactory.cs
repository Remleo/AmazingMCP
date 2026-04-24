using System.Text.RegularExpressions;

namespace AmazingMCP.Services;

/// <summary>
/// Default implementation of <see cref="IWildcardPatternFactory"/>.
/// </summary>
public sealed class WildcardPatternFactory : IWildcardPatternFactory
{
    const string SegmentPattern = @"[^,<> ]*?"; // stops at type-argument delimiters
    const string AnyPattern     = @".*";         // matches everything

    /// <inheritdoc/>
    public IWildcardPattern CreateForTypeNames(string pattern) =>
        new WildcardPattern(BuildRegex(pattern, segmentAware: true));

    /// <inheritdoc/>
    public IWildcardPattern CreateGlob(string pattern) =>
        new WildcardPattern(BuildRegex(pattern, segmentAware: false));

    static Regex BuildRegex(string pattern, bool segmentAware)
    {
        var parts = pattern.Split('*');
        var sb = new System.Text.StringBuilder("^");

        for (var i = 0; i < parts.Length; i++)
        {
            sb.Append(Regex.Escape(parts[i]));

            if (i < parts.Length - 1)
            {
                string wildcard;
                if (!segmentAware)
                {
                    wildcard = AnyPattern;
                }
                else
                {
                    var isLeading  = i == 0 && parts[0].Length == 0;
                    var isTrailing = i == parts.Length - 2 && parts[i + 1].Length == 0;
                    wildcard = (isLeading || isTrailing) ? AnyPattern : SegmentPattern;
                }

                sb.Append(wildcard);
            }
        }

        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }
}
