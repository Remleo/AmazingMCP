using AmazingMCP.Models.FileAnalysis;

namespace AmazingMCP.Models.UsageQuery;

/// <summary>
/// The traversal scope at the moment a <see cref="QueryEntry"/> was matched.
/// </summary>
/// <param name="MethodDefinitionRange">Line range of the method/property declaration (signature + opening brace). Null when outside a method.</param>
/// <param name="MethodFullRange">Full line range of the method/property including its body. Used for the annotation comment so readers know the total extent.</param>
/// <param name="Section">Null for synthetic (third-party) inheritance matches that have no source section.</param>
/// <param name="SyntheticDeclaration">Synthetic type declaration for third-party types that have no source file. Non-null only when <see cref="Section"/> is null.</param>
public sealed record UsageScope(
    string TypeName,
    string FilePath,
    string? MethodName,
    LineRange? MethodDefinitionRange,
    LineRange? MethodFullRange,
    ScopeSection? Section,
    int MatchLine,
    string? SyntheticDeclaration = null
);
