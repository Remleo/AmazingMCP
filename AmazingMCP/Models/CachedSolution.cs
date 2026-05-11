using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace AmazingMCP.Models;

public sealed class CachedSolution(
    MSBuildWorkspace workspace,
    Solution solution,
    List<(string ProjectName, Compilation Compilation)> compilations,
    ILogger? logger = null) : ICachedSolution, IDisposable
{
    readonly ConcurrentDictionary<string, bool> _dirtyFiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string ProjectName, Compilation Compilation)> Compilations => compilations.ToList();

    public Solution Solution => solution;

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

        var files = DrainDirtyFiles();
        if (files.Count == 0) return;

        logger?.LogInformation("EnsureUpToDate: processing {Count} dirty file(s): {Files}",
            files.Count, string.Join(", ", files));

        var affectedProjectIds = UpdateDocumentTexts(files);
        await RecompileAffectedProjects(affectedProjectIds);
    }

    List<string> DrainDirtyFiles()
    {
        var files = _dirtyFiles.Keys.ToList();
        foreach (var f in files) _dirtyFiles.TryRemove(f, out _);
        return files;
    }

    List<ProjectId> UpdateDocumentTexts(List<string> files)
    {
        foreach (var filePath in files)
        {
            var docId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            if (docId is null)
            {
                logger?.LogWarning("EnsureUpToDate: file not found in solution, skipping: {File}", filePath);
                continue;
            }

            var text = SourceText.From(File.ReadAllText(filePath));
            solution = solution.WithDocumentText(docId, text);
            logger?.LogInformation("EnsureUpToDate: updated document text for {File}", filePath);
        }

        var graph = solution.GetProjectDependencyGraph();
        return files
            .SelectMany(f => solution.GetDocumentIdsWithFilePath(f))
            .Select(d => d.ProjectId)
            .Distinct()
            .SelectMany(id => graph.GetProjectsThatTransitivelyDependOnThisProject(id).Append(id))
            .Distinct()
            .ToList();
    }

    async Task RecompileAffectedProjects(List<ProjectId> projectIds)
    {
        logger?.LogInformation("EnsureUpToDate: recompiling {Count} project(s)", projectIds.Count);

        foreach (var projectId in projectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                logger?.LogWarning("EnsureUpToDate: project not found for id {Id}, skipping", projectId);
                continue;
            }

            var newCompilation = await project.GetCompilationAsync();
            if (newCompilation is null)
            {
                logger?.LogWarning("EnsureUpToDate: GetCompilationAsync returned null for {Project}", project.Name);
                continue;
            }

            var idx = compilations.FindIndex(c => c.ProjectName == project.Name);
            if (idx >= 0)
                compilations[idx] = (project.Name, newCompilation);
            else
                compilations.Add((project.Name, newCompilation));

            logger?.LogInformation("EnsureUpToDate: recompiled {Project} successfully", project.Name);
        }
    }

    public void Dispose() => workspace.Dispose();
}
