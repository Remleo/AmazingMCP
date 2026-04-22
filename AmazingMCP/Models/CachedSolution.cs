using System.Collections.Concurrent;
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
    readonly ConcurrentDictionary<string, bool> _dirtyFiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string ProjectName, Compilation Compilation)> Compilations
    {
        get { lock (_lock) return compilations.ToList(); }
    }

    public Solution Solution
    {
        get { lock (_lock) return solution; }
    }

    /// <summary>
    /// Marks a file as dirty. Compilation will be deferred until <see cref="EnsureUpToDateAsync"/> is called.
    /// </summary>
    public void MarkDirty(string filePath) => _dirtyFiles.TryAdd(filePath, false);

    /// <summary>
    /// If there are dirty files, updates their text in the solution and recompiles only the affected projects.
    /// No-op if nothing is dirty.
    /// </summary>
    public async Task EnsureUpToDateAsync()
    {
        if (_dirtyFiles.IsEmpty) return;

        var files = _dirtyFiles.Keys.ToList();
        foreach (var f in files) _dirtyFiles.TryRemove(f, out _);

        // Update solution text for all dirty files
        lock (_lock)
        {
            foreach (var filePath in files)
            {
                var docId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
                if (docId is null) continue;

                var text = SourceText.From(File.ReadAllText(filePath));
                solution = solution.WithDocumentText(docId, text);
            }
        }

        // Collect unique affected project IDs
        var projectIds = files
            .SelectMany(f => solution.GetDocumentIdsWithFilePath(f))
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
    }
    public void Dispose() => workspace.Dispose();
}
