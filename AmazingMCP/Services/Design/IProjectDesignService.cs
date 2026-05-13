namespace AmazingMCP.Services.Design;

public interface IProjectDesignService
{
    Task<string> GetDesignAsync(string solutionPath, CancellationToken ct = default);
}
