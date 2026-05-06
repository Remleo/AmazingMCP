using AmazingMCP.Services;

namespace AmazingMCP.Tests;

class InMemoryFileReader : IFileReader
{
    readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string path, string content) => _files[path] = content;

    public bool Exists(string path) => _files.ContainsKey(path);
    public long GetLength(string path) => _files[path].Length;
    public string ReadAllText(string path) => _files[path];
    public string[] ReadAllLines(string path) => _files[path].Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
}
