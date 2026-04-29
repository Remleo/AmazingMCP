namespace AmazingMCP.Models;

/// <summary>
/// Represents a single resolved type entry collected from a code span.
/// </summary>
public sealed class CodeLensEntry
{
    public required CodeLensEntryKind Kind { get; init; }

    // Variable / Field / Property
    public string? VariableName { get; init; }
    public string? ResolvedType { get; init; }

    // Call / Extension / DefinitionMethod
    public string? MethodName { get; init; }
    public string? ReturnType { get; init; }
    public IReadOnlyList<string>? ArgTypes { get; init; }
    public IReadOnlyList<string>? ArgNames { get; init; }
    public int ArgCount { get; init; }

    // Extension: receiver type and original param name (first param with 'this')
    public string? ReceiverType { get; init; }
    public string? ReceiverParamName { get; init; }

    // Call / Extension: declaring type (shown as "from Type"), null = same class
    public string? DeclaringType { get; init; }

    // Constructor / DefinitionType: full qualified name
    public string? TypeFullName { get; init; }

    // Constructor / DefinitionType: short name without namespace (used in output)
    public string? TypeShortName { get; init; }

    // DefinitionType: base types and interfaces
    public IReadOnlyList<string>? BaseTypes { get; init; }

    /// <summary>Line number (1-based) where this entry first appeared in the source span.</summary>
    public int SourceLine { get; init; }
}
