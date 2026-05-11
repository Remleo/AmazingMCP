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

        var updatedSolution = ApplyFileChanges(solution, dirtyFiles);
        var affectedProjectIds = GetAffectedProjectIds(updatedSolution, solution, dirtyFiles);
        var updatedCompilations = await RecompileAffectedProjects(updatedSolution, compilations, affectedProjectIds);

        return (updatedSolution, updatedCompilations);
    }

    Solution ApplyFileChanges(Solution solution, IReadOnlyCollection<string> files)
    {
        foreach (var filePath in files)
        {
            var existingDocId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();

            solution = File.Exists(filePath)
                ? existingDocId is not null
                    ? UpdateDocument(solution, existingDocId, filePath)
                    : AddDocument(solution, filePath)
                : RemoveDocument(solution, existingDocId, filePath);
        }

        return solution;
    }

    Solution UpdateDocument(Solution solution, DocumentId docId, string filePath)
    {
        var text = SourceText.From(File.ReadAllText(filePath));
        logger.LogInformation("Recompiler: updated document {File}", filePath);
        return solution.WithDocumentText(docId, text);
    }

    Solution AddDocument(Solution solution, string filePath)
    {
        var project = FindProjectForFile(solution, filePath);
        if (project is null)
        {
            logger.LogWarning("Recompiler: no project found for new file, skipping: {File}", filePath);
            return solution;
        }

        var text = SourceText.From(File.ReadAllText(filePath));
        var docInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            name: Path.GetFileName(filePath),
            filePath: filePath,
            loader: TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create(), filePath)));

        logger.LogInformation("Recompiler: added document {File} to project {Project}", filePath, project.Name);
        return solution.AddDocument(docInfo);
    }

    Solution RemoveDocument(Solution solution, DocumentId? docId, string filePath)
    {
        if (docId is null)
        {
            logger.LogWarning("Recompiler: deleted file not found in solution, skipping: {File}", filePath);
            return solution;
        }

        logger.LogInformation("Recompiler: removed document {File}", filePath);
        return solution.RemoveDocument(docId);
    }

    static Project? FindProjectForFile(Solution solution, string filePath)
    {
        return solution.Projects
            .Where(p => p.FilePath is not null)
            .OrderByDescending(p => p.FilePath!.Length)
            .FirstOrDefault(p =>
                filePath.StartsWith(Path.GetDirectoryName(p.FilePath!)!, StringComparison.OrdinalIgnoreCase));
    }

    static List<ProjectId> GetAffectedProjectIds(Solution updatedSolution, Solution originalSolution, IReadOnlyCollection<string> files)
    {
        var graph = updatedSolution.GetProjectDependencyGraph();

        var directIds = files
            .SelectMany(f =>
                updatedSolution.GetDocumentIdsWithFilePath(f)
                    .Concat(originalSolution.GetDocumentIdsWithFilePath(f)))
            .Select(d => d.ProjectId)
            .Distinct();

        return directIds
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
