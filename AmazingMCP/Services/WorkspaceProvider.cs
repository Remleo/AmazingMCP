using System.Collections.Concurrent;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Caching.Memory;
using Nito.AsyncEx;

namespace AmazingMCP.Services;

/// <summary>
/// Caches MSBuildWorkspace + Compilations per solution path.
/// Watches .cs files for changes and incrementally updates compilations.
/// Unused solutions are evicted after a sliding timeout and their workspaces disposed.
/// </summary>
public sealed class WorkspaceProvider(IMemoryCache cache, ILogger<WorkspaceProvider> logger) : IWorkspaceProvider, IDisposable
{
    static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(15);

    readonly Lock _lock = new();

    readonly ConcurrentDictionary<string, List<FileSystemWatcher>> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ICachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        solutionPath = Path.GetFullPath(solutionPath);

        return await GetSolutionCoreAsync(solutionPath, ct);
    }

    async Task<CachedSolution> GetSolutionCoreAsync(string solutionPath, CancellationToken ct)
    {
        using var __ = await GetLocker(solutionPath).LockAsync();

        if (await TryGetFromCacheAsync(solutionPath) is { } cached)
            return cached;

        logger.LogInformation("Loading solution {Path}...", solutionPath);

        var entry = await LoadSolutionAsync(solutionPath, ct);

        StartWatcher(solutionPath);
        AddToCache(solutionPath, entry);

        return entry;
    }

    async Task<CachedSolution?> TryGetFromCacheAsync(string solutionPath)
    {
        if (TryGetCachedSolution(solutionPath, out var existing))
        {
            await existing.EnsureUpToDateAsync();
            return existing;
        }

        return null;
    }

    void AddToCache(string solutionPath, CachedSolution entry)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        var options = new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration };
        options.RegisterPostEvictionCallback((_, value, reason, _) => EvictSolutionCache(solutionPath, value, reason));

        cache.Set(new SolutionKey(solutionPath), entry, options);
    }

    void EvictSolutionCache(string solutionPath, object? value, EvictionReason reason)
    {
        if (value is not CachedSolution evicted) return;
        if (reason != EvictionReason.Expired) return;

        var locker = GetLocker(solutionPath);
        locker.Wait();

        try
        {
            // Try to acquire the lock immediately, but don't wait. If we can't acquire the lock, it means another operation is in progress and key gonna be prolonged
            if (TryGetCachedSolution(solutionPath, out var cached))
            {
                logger.LogInformation("Evicted solution cache ({Reason}) but key is still in use: {Path}", reason, solutionPath);
                if (!ReferenceEquals(evicted, cached))
                    cached.Dispose();
                return;
            }

            logger.LogInformation("Evicting solution cache ({Reason}): {Path}", reason, solutionPath);
            StopWatcher(solutionPath);
            evicted.Dispose();
        }
        finally
        {
            locker.Release();
        }
    }

    void Invalidate(string solutionPath)
    {
        StopWatcher(solutionPath);
        cache.Remove(new SolutionKey(solutionPath));
    }

    void StartWatcher(string solutionPath)
    {
        var dir = Path.GetDirectoryName(solutionPath)!;
        var watchers = new List<FileSystemWatcher>();

        watchers.Add(CreateSourceWatcher(dir, solutionPath));
        watchers.AddRange(CreateStructureWatchers(dir, solutionPath));

        _watchers[solutionPath] = watchers;
        logger.LogInformation("Started file watchers for {Dir}", dir);
    }

    FileSystemWatcher CreateSourceWatcher(string dir, string solutionPath)
    {
        var watcher = new FileSystemWatcher(dir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        watcher.Changed += (_, e) => OnSourceFileChanged(solutionPath, e.FullPath);
        watcher.Created += (_, e) => OnSourceFileChanged(solutionPath, e.FullPath);
        watcher.Renamed += (_, e) => OnSourceFileChanged(solutionPath, e.FullPath);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    IEnumerable<FileSystemWatcher> CreateStructureWatchers(string dir, string solutionPath)
    {
        foreach (var pattern in new[] { "*.csproj", "*.sln", "*.slnx" })
        {
            var watcher = new FileSystemWatcher(dir, pattern)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            watcher.Changed += (_, e) => OnProjectFileChanged(solutionPath, e.FullPath);
            watcher.Created += (_, e) => OnProjectFileChanged(solutionPath, e.FullPath);
            watcher.Renamed += (_, e) => OnProjectFileChanged(solutionPath, e.FullPath);
            watcher.Deleted += (_, e) => OnProjectFileChanged(solutionPath, e.FullPath);
            watcher.EnableRaisingEvents = true;
            yield return watcher;
        }
    }

    void StopWatcher(string solutionPath)
    {
        if (_watchers.TryRemove(solutionPath, out var watchers))
        {
            logger.LogInformation("Stopping watchers for solution: {Solution}", solutionPath);

            foreach (var w in watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
        }
    }

    void OnSourceFileChanged(string solutionPath, string filePath)
    {
        if (!TryGetCachedSolution(solutionPath, out var cached)) return;

        cached.MarkDirty(filePath);
        logger.LogInformation("Marked dirty: {File}", filePath);
    }

    void OnProjectFileChanged(string solutionPath, string filePath)
    {
        logger.LogInformation("Project/solution file changed: {File}, invalidating cache", filePath);
        Invalidate(solutionPath);
    }

    async Task<CachedSolution> LoadSolutionAsync(string fullPath, CancellationToken ct)
    {
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: ct);

        var compilations = new List<(string, Compilation)>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is not null)
                compilations.Add((project.Name, compilation));
        }

        return new(workspace, solution, compilations, logger);
    }

    public void Dispose()
    {
        foreach (var watchers in _watchers.Values)
        foreach (var w in watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }

        _watchers.Clear();
    }

    SemaphoreSlim GetLocker(string solutionPath)
    {
        lock (_lock)
        {
            return cache.GetOrCreate(new LockKey(solutionPath), _ => new SemaphoreSlim(1), new()
            {
                SlidingExpiration = SlidingExpiration * 2,
            })!;
        }
    }

    bool TryGetCachedSolution(string solutionPath, out CachedSolution cached)
    {
        if (!cache.TryGetValue(new SolutionKey(solutionPath), out cached)) return false;

        if (cached is null)
            throw new InvalidOperationException("Cached solution is null");

        return true;
    }

    record SolutionKey(string SolutionPath);

    record LockKey(string SolutionPath);
}