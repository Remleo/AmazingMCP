namespace AmazingMCP.Services.FileAnalysis;

public class FileSystemFileReader : IFileReader
{
    public bool Exists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
}
