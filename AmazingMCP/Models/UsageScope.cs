namespace AmazingMCP.Models;

/// <summary>
/// The traversal scope at the moment a <see cref="QueryEntry"/> was matched.
/// </summary>
public sealed record UsageScope(
    string TypeName,
    string FilePath,
    string? MethodName,
    /// <summary>Line range of the method/property declaration (signature + opening brace). Null when outside a method.</summary>
    LineRange? MethodDefinitionRange,
    /// <summary>Full line range of the method/property including its body. Used for the annotation comment so readers know the total extent.</summary>
    LineRange? MethodFullRange,
    ScopeSection Section,
    int MatchLine
);
