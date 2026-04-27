namespace AmazingMCP.Models;

/// <summary>
/// A matched usage entry together with its traversal scope.
/// </summary>
public sealed record UsageMatch(QueryEntry Entry, UsageScope Scope);
