namespace AmazingMCP.Services.Workspace;

/// <summary>
/// Resolves the .sln/.slnx file path from a workspace directory.
/// If exactly one solution is found, returns it automatically.
/// If multiple are found and no explicit path is given, returns an error listing them.
/// </summary>
public class SolutionResolver
{
    static readonly string[] SolutionPatterns = ["*.sln", "*.slnx"];

    public (string? SolutionPath, string? Error) Resolve(string workspacePath, string? solutionPath)
    {
        if (!string.IsNullOrWhiteSpace(solutionPath))
        {
            var full = Path.GetFullPath(solutionPath);
            if (!File.Exists(full))
                return (null, $"Specified solution file not found: {full}");
            return (full, null);
        }

        if (!Directory.Exists(workspacePath))
            return (null, $"Workspace directory not found: {workspacePath}");

        var solutions = SolutionPatterns
            .SelectMany(p => Directory.GetFiles(workspacePath, p, SearchOption.TopDirectoryOnly))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return solutions.Count switch
        {
            0 => (null, $"No .sln or .slnx files found in '{workspacePath}'."),
            1 => (Path.GetFullPath(solutions[0]), null),
            _ => (null,
                $"Multiple solution files found in '{workspacePath}'. " +
                $"Please specify the exact solution using the 'solutionPath' parameter. " +
                $"Available solutions:\n" +
                string.Join("\n", solutions.Select(s => $"  - {Path.GetFullPath(s)}")))
        };
    }
}
