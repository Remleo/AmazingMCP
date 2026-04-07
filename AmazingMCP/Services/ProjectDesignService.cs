using AmazingMCP.Models;

namespace AmazingMCP.Services;

/// <summary>
/// Builds a high-level project design: groups of abstractions and their inter-group dependencies.
/// Groups are formed by namespace hierarchy. For each group, external dependencies are resolved
/// to target group references by walking all implementations and their base class chains.
/// </summary>
public class ProjectDesignService(DependencyMapService dependencyMapService)
{
    public async Task<ProjectDesignResult> BuildAsync(
        string solutionPath, CancellationToken ct = default)
    {
        var depMap = await dependencyMapService.BuildMapAsync(solutionPath, ct);
        return BuildFromDependencyMap(depMap, solutionPath);
    }

    internal static ProjectDesignResult BuildFromDependencyMap(
        DependencyMapResult depMap, string solutionPath)
    {
        var rootNamespaces = ResolveRootNamespaces(solutionPath);

        var sortedRoots = rootNamespaces.Values
            .Distinct()
            .OrderByDescending(r => r.Length)
            .ThenBy(r => r)
            .ToList();

        // Phase 1: assign each abstraction to a namespace group (flat, no project split).
        // Abstractions without a source file (NuGet-only types) are excluded from the main
        // group list but still participate in dependency resolution (Phase 2 lookup).
        var groups = new Dictionary<string, List<AbstractionInfo>>();

        foreach (var abstraction in depMap.Abstractions.Values)
        {
            if (abstraction.SourceFilePath is null) continue; // NuGet type — skip from groups

            var ns = abstraction.Namespace;
            if (!groups.TryGetValue(ns, out var list))
            {
                list = [];
                groups[ns] = list;
            }
            list.Add(abstraction);
        }

        // Phase 2: build a lookup: abstraction full name → namespace group.
        // Includes ALL abstractions (including NuGet ones) so that dependencies on them
        // are resolved to the correct target group namespace.
        var abstractionToGroup = new Dictionary<string, string>();
        foreach (var abstraction in depMap.Abstractions.Values)
            abstractionToGroup[abstraction.FullName] = abstraction.Namespace;

        // Phase 3: build result with group-level dependencies
        var result = new List<AbstractionGroup>();

        foreach (var (ns, abstractions) in groups.OrderBy(kv => kv.Key))
        {
            var abstractionSet = abstractions.Select(a => a.FullName).ToHashSet();

            var rawExternalDeps = CollectExternalDependencies(abstractions, depMap, abstractionSet);

            // Resolve raw dep full names to target group full namespaces
            var depGroups = new HashSet<string>();
            foreach (var dep in rawExternalDeps)
            {
                if (abstractionToGroup.TryGetValue(dep, out var targetNs))
                    depGroups.Add(targetNs);
            }

            // Compute short name relative to owning project root namespace
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

    static HashSet<string> CollectExternalDependencies(
        List<AbstractionInfo> abstractions,
        DependencyMapResult depMap,
        HashSet<string> groupAbstractionSet)
    {
        var externalDeps = new HashSet<string>();

        foreach (var abstraction in abstractions)
        {
            // Walk implementations listed under this abstraction
            foreach (var implName in abstraction.Implementations)
                CollectDepsFromImplChain(implName, depMap, groupAbstractionSet, externalDeps);

            // Standalone classes: the abstraction itself may be in Implementations
            if (!abstraction.IsInterface)
                CollectDepsFromImplChain(abstraction.FullName, depMap, groupAbstractionSet, externalDeps);
        }

        return externalDeps;
    }

    static void CollectDepsFromImplChain(
        string implName,
        DependencyMapResult depMap,
        HashSet<string> groupAbstractionSet,
        HashSet<string> externalDeps)
    {
        if (!depMap.Implementations.TryGetValue(implName, out var impl))
            return;

        AddExternalDeps(impl, depMap, groupAbstractionSet, externalDeps);

        foreach (var baseClass in impl.BaseClasses)
        {
            if (depMap.Implementations.TryGetValue(baseClass, out var baseImpl))
                AddExternalDeps(baseImpl, depMap, groupAbstractionSet, externalDeps);
        }
    }

    static void AddExternalDeps(
        ImplementationInfo impl,
        DependencyMapResult depMap,
        HashSet<string> groupAbstractionSet,
        HashSet<string> externalDeps)
    {
        foreach (var dep in impl.Dependencies)
        {
            if (!groupAbstractionSet.Contains(dep.TypeFullName) &&
                depMap.Abstractions.ContainsKey(dep.TypeFullName))
                externalDeps.Add(dep.TypeFullName);
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
                var projectName = rootNamespaces
                    .FirstOrDefault(kv => kv.Value == rootNs).Key;
                if (projectName is not null)
                    return (projectName, rootNs);
            }
        }

        return (ns, ns);
    }

    internal static string GetRelativeNamespace(string ns, string rootNs)
    {
        if (string.IsNullOrEmpty(rootNs))
            return ns;

        if (ns == rootNs)
            return "";

        if (ns.StartsWith(rootNs + "."))
            return ns[(rootNs.Length + 1)..];

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
        catch
        {
            return null;
        }
    }
}
