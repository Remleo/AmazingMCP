using AmazingMCP.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AmazingMCP.Tests.Helpers;

/// <summary>
/// Opens the real TestSolution via MSBuild and provides a CachedSolution
/// backed by actual compilations from disk projects.
/// The solution is compiled once per test process and shared across all test classes.
/// </summary>
public static class CompilationHelper
{
    static readonly Lock InitLock = new();
    static bool _msbuildRegistered;

    static readonly string TestSolutionPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "TestData", "TestSolution", "TestSolution.slnx"));

    public static string SolutionPath => TestSolutionPath;
    public static string WorkspacePath => Path.GetDirectoryName(TestSolutionPath)!;

    // Shared across all test classes — compiled once per test process.
    static readonly Lazy<Task<CachedSolution>> SharedSolution =
        new(() => LoadAsync(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Returns the shared <see cref="CachedSolution"/> compiled once for the entire test run.
    /// Do NOT dispose the returned instance.
    /// </summary>
    public static Task<CachedSolution> GetSharedSolutionAsync() => SharedSolution.Value;

    /// <summary>
    /// Opens the test solution and returns a fresh <see cref="CachedSolution"/>.
    /// Caller is responsible for disposing the result.
    /// Use <see cref="GetSharedSolutionAsync"/> instead unless you need an isolated instance.
    /// </summary>
    public static Task<CachedSolution> LoadTestSolutionAsync(CancellationToken ct = default) => LoadAsync(ct);

    static async Task<CachedSolution> LoadAsync(CancellationToken ct = default)
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
