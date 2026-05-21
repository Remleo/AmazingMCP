namespace AmazingMCP.Services.Workspace;

public interface ISolutionResolver
{
    (string? SolutionPath, string? Error) Resolve(string workspacePath, string? solutionPath);
}
