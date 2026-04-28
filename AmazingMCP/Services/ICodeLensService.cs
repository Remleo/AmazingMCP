namespace AmazingMCP.Services;

public interface ICodeLensService
{
    Task<string> AnalyzeAsync(
        string solutionPath,
        string filePath,
        int startLine,
        int endLine,
        CancellationToken ct = default);
}
