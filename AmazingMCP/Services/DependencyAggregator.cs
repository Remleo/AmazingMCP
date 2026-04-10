using AmazingMCP.Models;

namespace AmazingMCP.Services;

public class DependencyAggregator : IDependencyAggregator
{
    public IReadOnlyList<AbstractionUsage> GetAllUsages(string implFullName, DependencyMapResult map)
    {
        var merged = new Dictionary<string, (bool IsStatic, HashSet<MemberUsage> Usages)>();
        CollectRecursive(implFullName, map, merged, new HashSet<string>());

        return merged
            .Select(kv => new AbstractionUsage(kv.Key, kv.Value.IsStatic, kv.Value.Usages.ToList()))
            .ToList();
    }

    static void CollectRecursive(
        string implName,
        DependencyMapResult map,
        Dictionary<string, (bool IsStatic, HashSet<MemberUsage> Usages)> merged,
        HashSet<string> visited)
    {
        if (!visited.Add(implName)) return;
        if (!map.Implementations.TryGetValue(implName, out var impl)) return;

        foreach (var dep in impl.Dependencies)
        {
            if (!merged.TryGetValue(dep.AbstractionFullName, out var entry))
            {
                entry = (dep.IsStatic, []);
                merged[dep.AbstractionFullName] = entry;
            }

            foreach (var usage in dep.Usages)
                entry.Usages.Add(usage);
        }

        foreach (var baseClass in impl.BaseClasses)
            CollectRecursive(baseClass, map, merged, visited);
    }
}
