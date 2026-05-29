using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery.Strategies;

namespace AmazingMCP.Services.SymbolQuery;

public interface IRoslynTypeProvider
{
    IEnumerable<T> GetAll<T>(ICachedSolution solution, ITypeEnumerationStrategy<T> strategy);
}
