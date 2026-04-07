namespace AmazingMCP.Models;

public record SymbolResult(
    string Name,
    string FullName,
    string Kind,
    string? ContainingAssembly,
    string? SourceFilePath = null,
    int? DefinitionLine = null);
