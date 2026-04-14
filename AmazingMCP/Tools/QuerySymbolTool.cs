using System.ComponentModel;
using AmazingMCP.Models;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class QuerySymbolTool
{
    [McpServerTool(Name = "query_symbol"), Description(
        "IMPORTANT: THIS TOOL CAN SEARCH TYPES FROM THIRD-PARTY NUGET PACKAGES — " +
        "USE THIS MCP WHEN YOU NEED TO FIND TYPES FROM EXTERNAL LIBRARIES. " +
        "Finds all types whose name contains the query string " +
        "across all projects in the given solution, including NuGet dependencies. " +
        "Searches nested public/internal classes as well. " +
        "Returns unique types (deduplicated across projects).")]
    public static async Task<string> QuerySymbol(
        RoslynSymbolService roslyn,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")] string solutionWorkspacePath,
        [Description("Type name (or part of it) to search for")] string query,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")] string? solutionPath = null,
        [Description("When false (default), only exact matches are returned if any exist. " +
                     "Set to true to include all partial (contains) matches as well.")]
        bool includePartialMatches = false,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null)
            return error!;

        var results = await roslyn.QuerySymbolsAsync(resolved, query, ct);

        if (results.Count == 0)
            return $"No types matching '{query}' found.";

        var exactMatches = results.Where(r => IsExactMatch(r, query)).ToList();

        if (exactMatches.Count > 0 && !includePartialMatches)
        {
            var output = FormatResults(exactMatches);
            var partialCount = results.Count - exactMatches.Count;

            if (partialCount > 0)
            {
                output += $"\n\n--- {exactMatches.Count} exact match(es) shown. " +
                          $"{partialCount} additional partial match(es) found. " +
                          "To see all results, repeat the same query with includePartialMatches = true.";
            }

            return output;
        }

        return FormatResults(results);
    }

    static bool IsExactMatch(SymbolResult result, string query)
    {
        if (query.Contains('.'))
            return result.FullName.EndsWith(query, StringComparison.OrdinalIgnoreCase);

        return result.Name.Equals(query, StringComparison.OrdinalIgnoreCase);
    }

    static string FormatResults(IEnumerable<SymbolResult> results) =>
        string.Join("\n", results.Select(r =>
            r.SourceFilePath is not null
                ? $"[{r.Kind}] {r.FullName}  (source: {r.SourceFilePath}, line {r.DefinitionLine})"
                : $"[{r.Kind}] {r.FullName}  (assembly: {r.ContainingAssembly})"));
}
