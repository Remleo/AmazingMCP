using System.Collections.Concurrent;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Caching.Memory;

namespace AmazingMCP.Services;

/// <summary>
/// Caches MSBuildWorkspace + Compilations per solution path.
/// Watches .cs files for changes and incrementally updates compilations.
/// Unused solutions are evicted after a sliding timeout and their workspaces disposed.
/// </summary>
public sealed class WorkspaceProvider(IMemoryCache cache, ILogger<WorkspaceProvider> logger) : IWorkspaceProvider, IDisposable
{
    static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);
    readonly ConcurrentDictionary<string, SemaphoreSlim> _loadGates = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, List<FileSystemWatcher>> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public async Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        var key = Path.GetFullPath(solutionPath);
        if (cache.TryGetValue<CachedSolution>(key, out var existing)) return existing!;

        var gate = _loadGates.GetOrAdd(key, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out existing)) return existing!;
            logger.LogInformation("Loading solution {Path}...", key);
            var entry = await LoadAsync(key, ct);

            var options = new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration };
            options.RegisterPostEvictionCallback((_, value, reason, _) =>
            {
                if (value is CachedSolution evicted)
                {
                    logger.LogInformation("Evicting solution cache ({Reason}): {Path}", reason, key);
                    StopWatcher(key);
                    evicted.Dispose();
                }
            });

            cache.Set(key, entry, options);
            StartWatcher(key);
            return entry;
        }
        finally { gate.Release(); }
    }

    public void Invalidate(string solutionPath)
    {
        var key = Path.GetFullPath(solutionPath);
        StopWatcher(key);
        cache.Remove(key);
    }

    void StartWatcher(string solutionKey)
    {
        var dir = Path.GetDirectoryName(solutionKey)!;
        var watchers = new List<FileSystemWatcher>();

        watchers.Add(CreateSourceWatcher(dir, solutionKey));
        watchers.AddRange(CreateStructureWatchers(dir, solutionKey));

        _watchers[solutionKey] = watchers;
        logger.LogInformation("Started file watchers for {Dir}", dir);
    }

    FileSystemWatcher CreateSourceWatcher(string dir, string solutionKey)
    {
        var watcher = new FileSystemWatcher(dir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        watcher.Changed += (_, e) => OnSourceFileChanged(solutionKey, e.FullPath);
        watcher.Created += (_, e) => OnSourceFileChanged(solutionKey, e.FullPath);
        watcher.Renamed += (_, e) => OnSourceFileChanged(solutionKey, e.FullPath);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    IEnumerable<FileSystemWatcher> CreateStructureWatchers(string dir, string solutionKey)
    {
        foreach (var pattern in new[] { "*.csproj", "*.sln", "*.slnx" })
        {
            var watcher = new FileSystemWatcher(dir, pattern)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            watcher.Changed += (_, e) => OnProjectFileChanged(solutionKey, e.FullPath);
            watcher.Created += (_, e) => OnProjectFileChanged(solutionKey, e.FullPath);
            watcher.Renamed += (_, e) => OnProjectFileChanged(solutionKey, e.FullPath);
            watcher.Deleted += (_, e) => OnProjectFileChanged(solutionKey, e.FullPath);
            watcher.EnableRaisingEvents = true;
            yield return watcher;
        }
    }

    void StopWatcher(string solutionKey)
    {
        if (_watchers.TryRemove(solutionKey, out var watchers))
        {
            foreach (var w in watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
        }
    }

    void OnSourceFileChanged(string solutionKey, string filePath)
    {
        if (!cache.TryGetValue<CachedSolution>(solutionKey, out var cached) || cached is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var updated = await cached.UpdateDocumentAsync(filePath);
                if (updated)
                    logger.LogInformation("Incremental recompile for {File}", filePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to incrementally update {File}, invalidating cache", filePath);
                Invalidate(solutionKey);
            }
        });
    }

    void OnProjectFileChanged(string solutionKey, string filePath)
    {
        logger.LogInformation("Project/solution file changed: {File}, invalidating cache", filePath);
        Invalidate(solutionKey);
    }

    static async Task<CachedSolution> LoadAsync(string fullPath, CancellationToken ct)
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

        return new(workspace, solution, compilations);
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
        foreach (var gate in _loadGates.Values) gate.Dispose();
        _loadGates.Clear();
    }
}
