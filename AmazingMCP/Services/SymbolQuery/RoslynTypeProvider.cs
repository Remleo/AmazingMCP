using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery.Strategies;
using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Enumerates named types across all compilations using a pluggable strategy.
/// </summary>
public class RoslynTypeProvider(NuGetVersionResolver versionResolver) : IRoslynTypeProvider
{
    public IEnumerable<T> GetAll<T>(ICachedSolution solution, ITypeEnumerationStrategy<T> strategy)
    {
        var seen = new Dictionary<object, T>();

        foreach (var (_, compilation) in solution.Compilations)
        {
            foreach (var type in RoslynTypeEnumerator.EnumerateAllInCompilation(compilation.GlobalNamespace))
            {
                var version = versionResolver.GetVersion(compilation, type);
                var key = strategy.GetKey(type, version);

                if (seen.TryGetValue(key, out var existing))
                    seen[key] = strategy.Merge(existing, type, version);
                else
                    seen[key] = strategy.Project(type, version);
            }
        }

        return seen.Values;
    }
}
