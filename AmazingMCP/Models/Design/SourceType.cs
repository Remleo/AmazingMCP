using Microsoft.CodeAnalysis;

namespace AmazingMCP.Models.Design;

/// <summary>
/// A source-defined type collected from a compilation.
/// </summary>
public record SourceType(INamedTypeSymbol Symbol, string ProjectName, Compilation Compilation);
