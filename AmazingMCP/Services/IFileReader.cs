namespace AmazingMCP.Services;

public interface IFileReader
{
    bool Exists(string path);
    long GetLength(string path);
    string ReadAllText(string path);
    string[] ReadAllLines(string path);
}
