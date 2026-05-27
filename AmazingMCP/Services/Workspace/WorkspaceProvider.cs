using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using Microsoft.Extensions.Caching.Memory;
using Nito.AsyncEx;

namespace AmazingMCP.Services.Workspace;

/// <summary>
/// Orchestrates solution loading, caching, file watching, and incremental recompilation.
/// </summary>
public sealed class WorkspaceProvider(
    ISolutionLoader loader,
    ISolutionCache cache,
    ISolutionWatcher watcher,
    ISolutionRecompiler recompiler,
    ILogger<WorkspaceProvider> logger) : IWorkspaceProvider, IDisposable
{
    readonly Lock _lock = new();
    readonly Dictionary<string, SemaphoreSlim> _lockers = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ICachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        solutionPath = Path.GetFullPath(solutionPath);

        using var __ = await GetLocker(solutionPath).LockAsync();

        if (cache.TryGet(solutionPath) is { } cached)
        {
            await RecompileIfDirtyAsync(cached);
            return cached;
        }

        logger.LogInformation("Loading solution {Path}...", solutionPath);

        var entry = await loader.LoadAsync(solutionPath, ct);

        watcher.Start(solutionPath,
            onSourceChanged: filePath => OnSourceFileChanged(solutionPath, filePath),
            onProjectChanged: filePath => OnProjectFileChanged(solutionPath, filePath));

        cache.Set(solutionPath, entry, onEvicted: evicted => OnSolutionEvicted(solutionPath, evicted));

        return entry;
    }

    async Task RecompileIfDirtyAsync(CachedSolution cached)
    {
        if (!cached.HasDirtyFiles) return;

        var dirtyFiles = cached.DrainDirtyFiles();
        var (updatedSolution, updatedCompilations) = await recompiler.RecompileAsync(
            cached.Solution, cached.Compilations, dirtyFiles);

        cached.Solution = updatedSolution;
        cached.Compilations = updatedCompilations;
    }

    void OnSolutionEvicted(string solutionPath, CachedSolution evicted)
    {
        logger.LogInformation("Evicting solution cache: {Path}", solutionPath);
        watcher.Stop(solutionPath);
        evicted.Dispose();
    }

    void OnSourceFileChanged(string solutionPath, string filePath)
    {
        if (cache.TryGet(solutionPath) is not { } cached) return;

        cached.MarkDirty(filePath);
        logger.LogInformation("Marked dirty: {File}", filePath);
    }

    void OnProjectFileChanged(string solutionPath, string filePath)
    {
        logger.LogInformation("Project/solution file changed: {File}, invalidating cache", filePath);
        cache.Invalidate(solutionPath);
        watcher.Stop(solutionPath);
    }

    SemaphoreSlim GetLocker(string solutionPath)
    {
        lock (_lock)
        {
            if (!_lockers.TryGetValue(solutionPath, out var locker))
                _lockers[solutionPath] = locker = new(1);
            return locker;
        }
    }

    public void Dispose() => watcher.Dispose();
}
