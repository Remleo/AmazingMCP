namespace AmazingMCP.Models.Design;

/// <summary>
/// Represents how a dependency's member is used.
/// </summary>
public enum MemberUsageKind
{
    MethodCall,
    PropertyGet,
    PropertySet
}
