using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;

namespace AmazingMCP.Services.Design;

/// <summary>
/// Filters out test project compilations from a solution.
/// A project is considered a test project if its .csproj references Microsoft.NET.Test.Sdk.
/// </summary>
public static class TestProjectFilter
{
    public static IReadOnlyList<(string ProjectName, Microsoft.CodeAnalysis.Compilation Compilation)>
        ExcludeTestProjects(
            IReadOnlyList<(string ProjectName, Microsoft.CodeAnalysis.Compilation Compilation)> compilations,
            ICachedSolution solution)
    {
        var testProjectNames = solution.Solution.Projects
            .Where(p => p.FilePath is not null && IsTestSdkInCsproj(p.FilePath))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (testProjectNames.Count == 0)
            return compilations;

        return compilations
            .Where(c => !testProjectNames.Contains(c.ProjectName))
            .ToList();
    }

    static bool IsTestSdkInCsproj(string csprojPath)
    {
        try
        {
            return File.ReadAllText(csprojPath)
                .Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
