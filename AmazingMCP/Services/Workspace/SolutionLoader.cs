using System.Reflection;
using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Polly;

namespace AmazingMCP.Services.Workspace;

public sealed class SolutionLoader : ISolutionLoader
{
    readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new()
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(400),
            MaxDelay = TimeSpan.FromSeconds(5),
            ShouldHandle = new PredicateBuilder()
                .Handle<ReflectionTypeLoadException>()
                .Handle<FileNotFoundException>(),
        })
        .Build();

    public async Task<CachedSolution> LoadAsync(string solutionPath, CancellationToken cancellationToken = default) =>
        await _pipeline.ExecuteAsync(async ct => await LoadCoreAsync(solutionPath, ct), cancellationToken);

    static async Task<CachedSolution> LoadCoreAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);

        var compilations = new List<(string, Compilation)>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is not null)
                compilations.Add((project.Name, compilation));
        }

        return new(workspace, solution, compilations);
    }
}
