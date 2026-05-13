namespace AmazingMCP.Services.FileAnalysis;

public interface IReadLargeCsFileService
{
    string Read(string filePath, string[]? filters);
}
