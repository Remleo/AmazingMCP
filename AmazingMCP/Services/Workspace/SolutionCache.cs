using AmazingMCP.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AmazingMCP.Services.Workspace;

public sealed class SolutionCache(IMemoryCache cache) : ISolutionCache
{
    static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(15);

    public CachedSolution? TryGet(string solutionPath) =>
        cache.TryGetValue(new SolutionKey(solutionPath), out CachedSolution? entry) ? entry : null;

    public void Set(string solutionPath, CachedSolution entry, Action<CachedSolution> onEvicted)
    {
        var options = new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration };
        options.RegisterPostEvictionCallback((_, value, reason, _) =>
        {
            if (value is CachedSolution evicted && reason == EvictionReason.Expired)
                onEvicted(evicted);
        });

        cache.Set(new SolutionKey(solutionPath), entry, options);
    }

    public void Invalidate(string solutionPath) =>
        cache.Remove(new SolutionKey(solutionPath));

    record SolutionKey(string SolutionPath);
}
