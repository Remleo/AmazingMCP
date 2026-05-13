using AmazingMCP.Models;
using AmazingMCP.Models.Design;

namespace AmazingMCP.Services.Design;

/// <summary>
/// Builds a high-level project design: groups of abstractions and their inter-group dependencies.
/// </summary>
public class ProjectDesignProvider(
    IDependencyMapService dependencyMapService,
    IDependencyAggregator dependencyAggregator) : IProjectDesignProvider
{
    public async Task<ProjectDesignResult> BuildAsync(
        string solutionPath, CancellationToken ct = default)
    {
        var depMap = await dependencyMapService.BuildMapAsync(solutionPath, ct);
        return BuildFromDependencyMap(depMap, solutionPath);
    }

    internal ProjectDesignResult BuildFromDependencyMap(
        DependencyMapResult depMap,
        string solutionPath)
    {
        var rootNamespaces = ResolveRootNamespaces(solutionPath);
        var sortedRoots = rootNamespaces.Values
            .Distinct()
            .OrderByDescending(r => r.Length)
            .ThenBy(r => r)
            .ToList();

        // Phase 1: group source abstractions by namespace (exclude external/NuGet)
        var groups = new Dictionary<string, List<AbstractionInfo>>();
        foreach (var abstraction in depMap.Abstractions.Values)
        {
            if (abstraction.SourceFilePath is null) continue;
            var ns = abstraction.Namespace;
            if (string.IsNullOrEmpty(ns)) continue; // global namespace (top-level statements etc.)
            if (!groups.TryGetValue(ns, out var list))
                groups[ns] = list = [];
            list.Add(abstraction);
        }

        // Phase 2: abstraction full name → namespace (includes NuGet for dep resolution)
        var abstractionToGroup = depMap.Abstractions.Values
            .ToDictionary(a => a.FullName, a => a.Namespace);

        // Phase 3: build result
        var result = new List<AbstractionGroup>();
        foreach (var (ns, abstractions) in groups.OrderBy(kv => kv.Key))
        {
            var abstractionSet = abstractions.Select(a => a.FullName).ToHashSet();
            var rawExternalDeps = CollectExternalDependencies(abstractions, depMap, abstractionSet);

            var depGroups = new HashSet<string>();
            foreach (var dep in rawExternalDeps)
                if (abstractionToGroup.TryGetValue(dep, out var targetNs))
                    depGroups.Add(targetNs);

            var (_, rootNs) = ResolveOwningProject(ns, rootNamespaces, sortedRoots);
            var shortName = GetRelativeNamespace(ns, rootNs);

            result.Add(new AbstractionGroup(
                FullName: ns,
                Name: shortName,
                EntryCount: abstractions.Count,
                DependsOn: depGroups.OrderBy(d => d).ToList()));
        }

        return new ProjectDesignResult(result);
    }

    HashSet<string> CollectExternalDependencies(
        List<AbstractionInfo> abstractions,
        DependencyMapResult depMap,
        HashSet<string> groupAbstractionSet)
    {
        var externalDeps = new HashSet<string>();

        foreach (var abstraction in abstractions)
        {
            foreach (var implName in abstraction.Implementations)
                CollectDepsFromImplChain(implName, depMap, groupAbstractionSet, externalDeps);

            // Standalone: abstraction is its own implementation
            if (!abstraction.IsInterface && !abstraction.IsAbstractClass)
                CollectDepsFromImplChain(abstraction.FullName, depMap, groupAbstractionSet, externalDeps);
        }

        return externalDeps;
    }

    void CollectDepsFromImplChain(
        string implName,
        DependencyMapResult depMap,
        HashSet<string> groupAbstractionSet,
        HashSet<string> externalDeps)
    {
        // Use aggregator to get all usages including base class chain
        var allUsages = dependencyAggregator.GetAllUsages(implName, depMap);
        foreach (var usage in allUsages)
        {
            if (!groupAbstractionSet.Contains(usage.AbstractionFullName) &&
                depMap.Abstractions.ContainsKey(usage.AbstractionFullName))
                externalDeps.Add(usage.AbstractionFullName);
        }
    }

    internal static (string ProjectName, string RootNs) ResolveOwningProject(
        string ns,
        Dictionary<string, string> rootNamespaces,
        List<string> sortedRoots)
    {
        foreach (var rootNs in sortedRoots)
        {
            if (ns == rootNs || ns.StartsWith(rootNs + "."))
            {
                var projectName = rootNamespaces.FirstOrDefault(kv => kv.Value == rootNs).Key;
                if (projectName is not null)
                    return (projectName, rootNs);
            }
        }
        return (ns, ns);
    }

    internal static string GetRelativeNamespace(string ns, string rootNs)
    {
        if (string.IsNullOrEmpty(rootNs)) return ns;
        if (ns == rootNs) return "";
        if (ns.StartsWith(rootNs + ".")) return ns[(rootNs.Length + 1)..];
        return ns;
    }

    internal static Dictionary<string, string> ResolveRootNamespaces(string solutionPath)
    {
        var result = new Dictionary<string, string>();
        var solutionDir = Path.GetDirectoryName(solutionPath);
        if (solutionDir is null || !Directory.Exists(solutionDir)) return result;

        foreach (var csproj in Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(csproj);
            result[projectName] = ExtractRootNamespace(csproj) ?? projectName;
        }

        return result;
    }

    internal static string? ExtractRootNamespace(string csprojPath)
    {
        try
        {
            var content = File.ReadAllText(csprojPath);
            const string startTag = "<RootNamespace>";
            const string endTag = "</RootNamespace>";

            var startIdx = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (startIdx < 0) return null;
            startIdx += startTag.Length;

            var endIdx = content.IndexOf(endTag, startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0) return null;

            var value = content[startIdx..endIdx].Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch { return null; }
    }
}
