namespace AmazingMCP.Services.Decompile;

public interface IDecompileTypeService
{
    Task<string> DecompileTypeAsync(
        string solutionPath,
        string fullTypeName,
        string[]? memberFilters = null,
        string? version = null,
        CancellationToken ct = default);
}
