namespace AmazingMCP.Services.FileAnalysis;

public interface IFileReader
{
    bool Exists(string path);
    string ReadAllText(string path);
}
