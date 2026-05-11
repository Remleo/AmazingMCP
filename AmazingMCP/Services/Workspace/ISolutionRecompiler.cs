using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.Workspace;

public interface ISolutionRecompiler
{
    /// <summary>
    /// Updates document texts for the given dirty files and recompiles affected projects.
    /// Returns the updated solution and compilations.
    /// </summary>
    Task<(Solution UpdatedSolution, List<(string ProjectName, Compilation Compilation)> UpdatedCompilations)>
        RecompileAsync(
            Solution solution,
            IReadOnlyCollection<(string ProjectName, Compilation Compilation)> compilations,
            IReadOnlyCollection<string> dirtyFiles);
}
