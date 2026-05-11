using System.Collections.Concurrent;

namespace AmazingMCP.Services.Workspace;

public sealed class SolutionWatcher(ILogger<SolutionWatcher> logger) : ISolutionWatcher
{
    readonly ConcurrentDictionary<string, List<FileSystemWatcher>> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public void Start(string solutionPath, Action<string> onSourceChanged, Action<string> onProjectChanged)
    {
        var dir = Path.GetDirectoryName(solutionPath)!;
        var watchers = new List<FileSystemWatcher>
        {
            CreateSourceWatcher(dir, onSourceChanged),
        };
        watchers.AddRange(CreateStructureWatchers(dir, onProjectChanged));

        _watchers[solutionPath] = watchers;
        logger.LogInformation("Started file watchers for {Dir}", dir);
    }

    public void Stop(string solutionPath)
    {
        if (!_watchers.TryRemove(solutionPath, out var watchers)) return;

        logger.LogInformation("Stopping watchers for solution: {Solution}", solutionPath);
        foreach (var w in watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var (_, watchers) in _watchers)
        foreach (var w in watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }

        _watchers.Clear();
    }

    static FileSystemWatcher CreateSourceWatcher(string dir, Action<string> onChanged)
    {
        var watcher = new FileSystemWatcher(dir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        watcher.Changed += (_, e) => onChanged(e.FullPath);
        watcher.Created += (_, e) => onChanged(e.FullPath);
        watcher.Renamed += (_, e) => onChanged(e.FullPath);
        watcher.Deleted += (_, e) => onChanged(e.FullPath);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    static IEnumerable<FileSystemWatcher> CreateStructureWatchers(string dir, Action<string> onChanged)
    {
        foreach (var pattern in new[] { "*.csproj", "*.sln", "*.slnx" })
        {
            var watcher = new FileSystemWatcher(dir, pattern)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            watcher.Changed += (_, e) => onChanged(e.FullPath);
            watcher.Created += (_, e) => onChanged(e.FullPath);
            watcher.Renamed += (_, e) => onChanged(e.FullPath);
            watcher.Deleted += (_, e) => onChanged(e.FullPath);
            watcher.EnableRaisingEvents = true;
            yield return watcher;
        }
    }
}
