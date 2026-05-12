using System.Text.RegularExpressions;
using AmazingMCP.Services.Wildcard;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Converts fully-qualified type names into wildcard patterns for use with <see cref="WildcardPatternFactory"/>.
///
/// Supported input formats:
///   "Foo.Bar`2"        → "Foo.Bar&lt;*, *&gt;"   (CLR metadata notation)
///   "Foo.Bar&lt;T, TVal&gt;" → "Foo.Bar&lt;*, *&gt;"   (C# generic syntax)
///   "Foo.Bar&lt;*,*&gt;"     → "Foo.Bar&lt;*,*&gt;"   (already wildcard — unchanged)
///   "Foo.Bar"          → "Foo.Bar"          (non-generic — unchanged)
/// </summary>
internal static class TypeWildcardPatternBuilder
{
    // Captures base name and arity from CLR backtick notation, e.g. "Foo.Bar`2" → ("Foo.Bar", "2")
    static readonly Regex BacktickPattern = new(@"^(.*)`(\d+)$", RegexOptions.Compiled);

    // Captures everything between the outermost '<' and '>', e.g. "<TModel, TVal>" or "<Outer<T>, T2>"
    static readonly Regex AngleBracketPattern = new(@"<(.+)>$", RegexOptions.Compiled | RegexOptions.Singleline);

    internal static string Build(string fullTypeName)
    {
        var m = BacktickPattern.Match(fullTypeName);
        if (m.Success)
            return FromBacktick(m);

        return AngleBracketPattern.Replace(fullTypeName, FromAngleBracket);
    }

    static string FromBacktick(Match m)
    {
        var arity = int.Parse(m.Groups[2].Value);
        return arity > 0
            ? $"{m.Groups[1].Value}<{WildcardArgs(arity)}>"
            : m.Groups[1].Value;
    }

    static string FromAngleBracket(Match match)
    {
        var inner = match.Groups[1].Value;

        // Count top-level commas to determine arity.
        // Manual depth tracking is required because regex cannot handle arbitrary nesting depth.
        var depth = 0;
        var arity = 1;
        foreach (var c in inner)
        {
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0) arity++;
        }

        return $"<{WildcardArgs(arity)}>";
    }

    static string WildcardArgs(int arity) => string.Join(", ", Enumerable.Repeat("*", arity));
}
