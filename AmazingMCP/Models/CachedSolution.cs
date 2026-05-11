using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AmazingMCP.Models;

public sealed class CachedSolution(
    MSBuildWorkspace workspace,
    Solution solution,
    List<(string ProjectName, Compilation Compilation)> compilations) : ICachedSolution, IDisposable
{
    readonly ConcurrentDictionary<string, bool> _dirtyFiles = new(StringComparer.OrdinalIgnoreCase);

    public Solution Solution { get; set; } = solution;

    public List<(string ProjectName, Compilation Compilation)> Compilations { get; set; } = compilations;

    IReadOnlyList<(string ProjectName, Compilation Compilation)> ICachedSolution.Compilations => Compilations;

    public bool HasDirtyFiles => !_dirtyFiles.IsEmpty;

    /// <summary>
    /// Marks a file as dirty. Recompilation will be deferred until the workspace provider processes it.
    /// </summary>
    public void MarkDirty(string filePath) => _dirtyFiles.TryAdd(filePath, false);

    /// <summary>
    /// Drains and returns all dirty file paths, clearing the dirty set.
    /// </summary>
    public List<string> DrainDirtyFiles()
    {
        var files = _dirtyFiles.Keys.ToList();
        foreach (var f in files) _dirtyFiles.TryRemove(f, out _);
        return files;
    }

    public void Dispose() => workspace.Dispose();
}
