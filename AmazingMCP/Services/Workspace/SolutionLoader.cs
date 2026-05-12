using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AmazingMCP.Services.Workspace;

public sealed class SolutionLoader : ISolutionLoader
{
    public async Task<CachedSolution> LoadAsync(string solutionPath, CancellationToken ct = default)
    {
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: ct);

        var compilations = new List<(string, Compilation)>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is not null)
                compilations.Add((project.Name, compilation));
        }

        return new(workspace, solution, compilations);
    }
}
