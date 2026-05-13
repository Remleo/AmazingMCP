namespace AmazingMCP.Services.Design;

public interface IProjectDesignDetailsService
{
    Task<string> GetDetailsAsync(
        string solutionPath,
        string[] forNamespaces,
        bool includeDependencyUsage,
        bool includeImplementations,
        CancellationToken ct = default);
}
