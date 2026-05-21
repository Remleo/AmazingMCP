namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Resolved target type information needed for inheritance search.
/// Can represent both open generic (IRepository&lt;T&gt;) and closed generic (IRepository&lt;Animal&gt;) targets.
/// </summary>
public sealed record InheritanceSearchSymbol(
    string FullName,
    bool IsFromSource,
    bool IsInterface,
    bool IsOpenGeneric);
