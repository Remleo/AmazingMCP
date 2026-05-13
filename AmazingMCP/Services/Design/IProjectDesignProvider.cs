using AmazingMCP.Models.Design;

namespace AmazingMCP.Services.Design;

public interface IProjectDesignProvider
{
    Task<ProjectDesignResult> BuildAsync(string solutionPath, CancellationToken ct = default);
}
