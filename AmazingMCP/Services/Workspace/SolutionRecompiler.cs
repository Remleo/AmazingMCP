using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AmazingMCP.Services.Workspace;

public sealed class SolutionRecompiler(ILogger<SolutionRecompiler> logger) : ISolutionRecompiler
{
    public async Task<(Solution UpdatedSolution, List<(string ProjectName, Compilation Compilation)> UpdatedCompilations)>
        RecompileAsync(
            Solution solution,
            IReadOnlyCollection<(string ProjectName, Compilation Compilation)> compilations,
            IReadOnlyCollection<string> dirtyFiles)
    {
        logger.LogInformation("Recompiler: processing {Count} dirty file(s): {Files}",
            dirtyFiles.Count, string.Join(", ", dirtyFiles));

        var updatedSolution = UpdateDocumentTexts(solution, dirtyFiles);
        var affectedProjectIds = GetAffectedProjectIds(updatedSolution, dirtyFiles);
        var updatedCompilations = await RecompileAffectedProjects(updatedSolution, compilations, affectedProjectIds);

        return (updatedSolution, updatedCompilations);
    }

    Solution UpdateDocumentTexts(Solution solution, IReadOnlyCollection<string> files)
    {
        foreach (var filePath in files)
        {
            var docId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            if (docId is null)
            {
                logger.LogWarning("Recompiler: file not found in solution, skipping: {File}", filePath);
                continue;
            }

            var text = SourceText.From(File.ReadAllText(filePath));
            solution = solution.WithDocumentText(docId, text);
            logger.LogInformation("Recompiler: updated document text for {File}", filePath);
        }

        return solution;
    }

    static List<ProjectId> GetAffectedProjectIds(Solution solution, IReadOnlyCollection<string> files)
    {
        var graph = solution.GetProjectDependencyGraph();
        return files
            .SelectMany(f => solution.GetDocumentIdsWithFilePath(f))
            .Select(d => d.ProjectId)
            .Distinct()
            .SelectMany(id => graph.GetProjectsThatTransitivelyDependOnThisProject(id).Append(id))
            .Distinct()
            .ToList();
    }

    async Task<List<(string ProjectName, Compilation Compilation)>> RecompileAffectedProjects(
        Solution solution,
        IReadOnlyCollection<(string ProjectName, Compilation Compilation)> compilations,
        List<ProjectId> projectIds)
    {
        logger.LogInformation("Recompiler: recompiling {Count} project(s)", projectIds.Count);

        var result = compilations.ToList();

        foreach (var projectId in projectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                logger.LogWarning("Recompiler: project not found for id {Id}, skipping", projectId);
                continue;
            }

            var newCompilation = await project.GetCompilationAsync();
            if (newCompilation is null)
            {
                logger.LogWarning("Recompiler: GetCompilationAsync returned null for {Project}", project.Name);
                continue;
            }

            var idx = result.FindIndex(c => c.ProjectName == project.Name);
            if (idx >= 0)
                result[idx] = (project.Name, newCompilation);
            else
                result.Add((project.Name, newCompilation));

            logger.LogInformation("Recompiler: recompiled {Project} successfully", project.Name);
        }

        return result;
    }
}
