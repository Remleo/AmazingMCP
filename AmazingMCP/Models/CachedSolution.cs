using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace AmazingMCP.Models;

public sealed class CachedSolution(
    MSBuildWorkspace workspace,
    Solution solution,
    List<(string ProjectName, Compilation Compilation)> compilations) : IDisposable
{
    readonly Lock _lock = new();

    public IReadOnlyList<(string ProjectName, Compilation Compilation)> Compilations
    {
        get { lock (_lock) return compilations.ToList(); }
    }

    public Solution Solution
    {
        get { lock (_lock) return solution; }
    }

    /// <summary>
    /// Finds the document by file path, replaces its text, and recompiles only the affected project.
    /// Returns true if the document was found and updated.
    /// </summary>
    public async Task<bool> UpdateDocumentAsync(string filePath)
    {
        lock (_lock)
        {
            var docId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            if (docId is null) return false;

            var text = SourceText.From(File.ReadAllText(filePath));
            solution = solution.WithDocumentText(docId, text);
        }

        // Recompile only the affected project(s)
        var projectIds = solution.GetDocumentIdsWithFilePath(filePath)
            .Select(d => d.ProjectId)
            .Distinct();

        foreach (var projectId in projectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null) continue;

            var newCompilation = await project.GetCompilationAsync();
            if (newCompilation is null) continue;

            lock (_lock)
            {
                var idx = compilations.FindIndex(c => c.ProjectName == project.Name);
                if (idx >= 0)
                    compilations[idx] = (project.Name, newCompilation);
                else
                    compilations.Add((project.Name, newCompilation));
            }
        }

        return true;
    }

    public void Dispose() => workspace.Dispose();
}
