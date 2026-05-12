using Microsoft.CodeAnalysis;

namespace AmazingMCP.Models.UsageQuery;

/// <summary>
/// Represents the nearest meaningful ancestor syntax node that defines
/// the code section containing a usage. Carries the pre-computed line range.
/// </summary>
public sealed record ScopeSection(SyntaxNode Node, int StartLine, int EndLine);
