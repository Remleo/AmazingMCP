using System.ComponentModel;
using AmazingMCP.Services;
using AmazingMCP.Services.CodeLens;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class CodeLensTool
{
    [McpServerTool(Name = "code_lens", ReadOnly = true), Description(
        "Resolves all type names in a given line range of a .cs file to their fully qualified forms — " +
        "with namespace and generic arguments. " +
        "Use this to see the exact full name of any type appearing in a code span: " +
        "a local variable, a method call, a class declaration, or anything else. " +
        "System.* namespaces are trimmed to short names. Primitive types are omitted.")]
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
