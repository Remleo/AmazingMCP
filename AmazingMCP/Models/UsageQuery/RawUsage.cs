using AmazingMCP.Models.Design;

namespace AmazingMCP.Models.UsageQuery;

/// <summary>
/// A dependency usage discovered during class body scanning.
/// Contains the ready-to-use AbstractionUsage plus the RawTypeInfo needed
/// to register the abstraction without re-parsing the type name.
/// Used only during map construction; not stored in the final DependencyMapResult.
/// </summary>
public record RawUsage(
    AbstractionUsage Usage,
    RawTypeInfo TypeInfo);
