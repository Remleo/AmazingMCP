using AmazingMCP.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AmazingMCP.Tests.Helpers;

/// <summary>
/// Opens the real TestSolution via MSBuild and provides a CachedSolution
/// backed by actual compilations from disk projects.
/// </summary>
public static class CompilationHelper
{
    static readonly Lock InitLock = new();
    static bool _msbuildRegistered;

    static readonly string TestSolutionPath = /*"C:\\dotNet\\BetContentAggregatorV2\\Source\\BetContentAggregatorV2.sln";*/ Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "TestData", "TestSolution", "TestSolution.slnx"));

    public static string SolutionPath => TestSolutionPath;

    public static string WorkspacePath => Path.GetDirectoryName(TestSolutionPath)!;

    /// <summary>
    /// Opens the test solution and returns a CachedSolution with real compilations.
    /// Caller is responsible for disposing the result.
    /// </summary>
    public static async Task<CachedSolution> LoadTestSolutionAsync(CancellationToken ct = default)
    {
        EnsureMSBuildRegistered();

        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(TestSolutionPath, cancellationToken: ct);

        var compilations = new List<(string, Compilation)>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is not null)
                compilations.Add((project.Name, compilation));
        }

        return new CachedSolution(workspace, solution, compilations);
    }

    static void EnsureMSBuildRegistered()
    {
        lock (InitLock)
        {
            if (_msbuildRegistered) return;
            MSBuildLocator.RegisterDefaults();
            _msbuildRegistered = true;
        }
    }
}
