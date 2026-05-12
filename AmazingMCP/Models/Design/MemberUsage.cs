namespace AmazingMCP.Models.Design;

/// <summary>
/// A single member usage: method call or property access on a dependency.
/// </summary>
public record MemberUsage(string MemberName, MemberUsageKind Kind)
{
    public override string ToString() => Kind switch
    {
        MemberUsageKind.PropertyGet => $"{MemberName} {{ get; }}",
        MemberUsageKind.PropertySet => $"{MemberName} {{ set; }}",
        _ => $"{MemberName}()"
    };
}
