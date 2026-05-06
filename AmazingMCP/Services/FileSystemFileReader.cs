namespace AmazingMCP.Services;

public class FileSystemFileReader : IFileReader
{
    public bool Exists(string path) => File.Exists(path);
    public long GetLength(string path) => new FileInfo(path).Length;
    public string ReadAllText(string path) => File.ReadAllText(path);
    public string[] ReadAllLines(string path) => File.ReadAllLines(path);
}
