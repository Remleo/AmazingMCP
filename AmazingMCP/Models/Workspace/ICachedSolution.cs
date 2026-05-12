using Microsoft.CodeAnalysis;

namespace AmazingMCP.Models.Workspace;

public interface ICachedSolution
{
    Solution Solution { get; }
    IReadOnlyList<(string ProjectName, Compilation Compilation)> Compilations { get; }
}
