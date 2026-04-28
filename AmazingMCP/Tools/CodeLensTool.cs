using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class CodeLensTool
{
    [McpServerTool(Name = "code_lens", ReadOnly = true), Description(
        "Analyzes a span of C# source code and resolves all type names to their fully qualified forms. " +
        "For each local variable, method call, extension method, constructor call, and definition " +
        "found in the given line range, returns the full type names (with namespace and generic arguments). " +
        "System.* namespaces are trimmed to short names. Primitive types are omitted. " +
        "Useful for understanding what types are actually used in a code block without navigating the full solution.")]
    public static async Task<string> CodeLens(
        ICodeLensService codeLensService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")]
        string solutionWorkspacePath,
        [Description("Absolute or relative path to the .cs file to analyze")]
        string filePath,
        [Description("First line of the range to analyze (1-based, inclusive)")]
        int startLine,
        [Description("Last line of the range to analyze (1-based, inclusive)")]
        int endLine,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")]
        string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolvedSolution, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolvedSolution == null)
            return error ?? "Could not resolve solution path.";

        return await codeLensService.AnalyzeAsync(resolvedSolution, filePath, startLine, endLine, ct);
    }
}
