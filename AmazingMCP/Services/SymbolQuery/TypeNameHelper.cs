namespace AmazingMCP.Services.SymbolQuery;

/// <summary>Helpers for working with type-name strings.</summary>
internal static class TypeNameHelper
{
    /// <summary>
    /// Extracts the simple type name from a possibly fully-qualified, possibly generic type name.
    /// Strips the generic part first (everything from the first '&lt;' or '`'), then the namespace
    /// (everything up to the last '.'). Stripping generics first is required because generic
    /// arguments may themselves contain dots and angle brackets.
    /// </summary>
    /// <example>
    ///   "MyApp.Core.IRequestStream"                          → "IRequestStream"
    ///   "System.Collections.Generic.List&lt;MyApp.Animal&gt;" → "List"
    ///   "Foo.Bar`2"                                          → "Bar"
    ///   "IRequestStream"                                     → "IRequestStream"
    /// </example>
    public static string GetSimpleName(string typeName)
    {
        var withoutGenerics = StripGenerics(typeName);

        var lastDot = withoutGenerics.LastIndexOf('.');
        return lastDot >= 0 ? withoutGenerics[(lastDot + 1)..] : withoutGenerics;
    }

    static string StripGenerics(string typeName)
    {
        var cut = typeName.IndexOfAny(['<', '`']);
        return cut >= 0 ? typeName[..cut] : typeName;
    }
}
