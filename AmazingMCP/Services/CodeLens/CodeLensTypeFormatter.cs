using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.CodeLens;

/// <summary>
/// Converts Roslyn type symbols to display strings,
/// trimming System.* namespace prefixes to short names.
/// </summary>
public static class CodeLensTypeFormatter
{
    /// <summary>
    /// Returns the fully qualified display name of a type,
    /// with System.* namespaces trimmed to their short names.
    /// </summary>
    public static string GetDisplayName(ITypeSymbol type)
    {
        var full = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);
        return TrimSystemNamespace(full);
    }

    /// <summary>
    /// Removes System.* namespace prefixes from a type name, recursively handling generic arguments.
    /// E.g. "System.Collections.Generic.IEnumerable&lt;MyApp.Core.Item&gt;" → "IEnumerable&lt;MyApp.Core.Item&gt;"
    /// </summary>
    public static string TrimSystemNamespace(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;

        var genericStart = typeName.IndexOf('<');
        if (genericStart >= 0)
        {
            var outer = typeName[..genericStart];
            var inner = typeName[(genericStart + 1)..^1];
            var trimmedOuter = TrimSystemPrefix(outer);
            var trimmedInner = TrimGenericArgs(inner);
            return $"{trimmedOuter}<{trimmedInner}>";
        }

        return TrimSystemPrefix(typeName);
    }

    static string TrimGenericArgs(string args)
    {
        var parts = SplitTopLevel(args);
        return string.Join(", ", parts.Select(TrimSystemNamespace));
    }

    static string TrimSystemPrefix(string name)
    {
        if (!name.StartsWith("System.", StringComparison.Ordinal)) return name;
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }

    internal static List<string> SplitTopLevel(string s)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '<') depth++;
            else if (s[i] == '>') depth--;
            else if (s[i] == ',' && depth == 0)
            {
                result.Add(s[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(s[start..].Trim());
        return result;
    }
}
