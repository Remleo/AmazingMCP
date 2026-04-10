namespace AmazingMCP.Models;

public record SymbolResult(
    string Name,
    string FullName,
    string Kind,
    string? ContainingAssembly,
    string? SourceFilePath,
    int? DefinitionLine);
