namespace AmazingMCP.Models;

/// <summary>
/// Represents a single resolved type entry collected from a code span.
/// </summary>
public sealed class CodeLensEntry
{
    public required CodeLensEntryKind Kind { get; init; }

    // Variable
    public string? VariableName { get; init; }

    // Call / Extension / Constructor / DefinitionMethod
    public string? MethodName { get; init; }
    public string? ReturnType { get; init; }
    public IReadOnlyList<string>? ArgTypes { get; init; }
    public int ArgCount { get; init; }

    // Extension only
    public string? ReceiverType { get; init; }

    // Constructor / DefinitionType
    public string? TypeFullName { get; init; }

    // DefinitionType: base types and interfaces visible in the span
    public IReadOnlyList<string>? BaseTypes { get; init; }

    // Variable / Call / Extension / Constructor / DefinitionMethod / DefinitionType
    public string? ResolvedType { get; init; }
}
