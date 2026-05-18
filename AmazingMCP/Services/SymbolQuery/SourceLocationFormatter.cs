using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>Formats source file location(s) for display.</summary>
public static class SourceLocationFormatter
{
    /// <summary>
    /// Formats a location comment from a list of source file paths.
    /// Single file: "// source: path/File.cs, line N"
    /// Multiple files: "// source: path/File.cs, File.Partial.cs | path2/Other.cs"
    /// Assembly fallback: "// assembly: AssemblyName"
    /// </summary>
    public static string FormatLocation(IReadOnlyList<string> paths, string? assemblyName, int? singleFileLine = null)
    {
        if (paths.Count == 0)
            return $"// assembly: {assemblyName}";

        if (paths.Count == 1)
            return singleFileLine.HasValue
                ? $"// source: {paths[0]}, line {singleFileLine}"
                : $"// source: {paths[0]}";

        var groups = paths
            .GroupBy(Path.GetDirectoryName)
            .Select(g =>
            {
                var first = g.First();
                var rest = g.Skip(1).Select(Path.GetFileName);
                return string.Join(", ", [first, ..rest]);
            });

        return $"// source: {string.Join(" | ", groups)}";
    }

    /// <summary>Extracts non-generated source paths from a symbol's syntax references.</summary>
    public static IReadOnlyList<string> GetSourcePaths(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences
            .Select(r => r.SyntaxTree.FilePath)
            .Where(p => !string.IsNullOrEmpty(p) && !p.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

    /// <summary>Formats location for a named type symbol.</summary>
    public static string FormatTypeLocation(INamedTypeSymbol type)
    {
        var paths = GetSourcePaths(type);

        int? line = null;
        if (paths.Count == 1)
        {
            var syntaxRef = type.DeclaringSyntaxReferences.First(r => r.SyntaxTree.FilePath == paths[0]);
            line = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span).StartLinePosition.Line + 1;
        }

        return FormatLocation(paths, type.ContainingAssembly?.Name, line);
    }
}
