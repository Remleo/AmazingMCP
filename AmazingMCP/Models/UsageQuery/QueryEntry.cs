namespace AmazingMCP.Models.UsageQuery;

/// <summary>
/// Represents a single usage occurrence found during solution traversal.
/// <see cref="TypeName"/> is always populated and identifies the target type involved in the usage.
/// Other fields are populated based on <see cref="UsageKind"/>; unused fields are null.
/// </summary>
public sealed class QueryEntry
{
    public required UsageKind Kind { get; init; }

    /// <summary>
    /// Full name of the target type involved in this usage.
    /// For method/property/field usages — the declaring type.
    /// For generic argument / constraint / return type usages — the referenced type itself.
    /// Always populated.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>Method name for <see cref="UsageKind.MethodCall"/> and <see cref="UsageKind.ConstructorCall"/> (type name).</summary>
    public string? MethodName { get; init; }

    /// <summary>Argument type names for <see cref="UsageKind.MethodCall"/> and <see cref="UsageKind.ConstructorCall"/>.</summary>
    public IReadOnlyList<string>? ArgumentTypes { get; init; }

    /// <summary>Property name for <see cref="UsageKind.PropertyRead"/> and <see cref="UsageKind.PropertyWrite"/>.</summary>
    public string? PropertyName { get; init; }

    /// <summary>Field name for <see cref="UsageKind.FieldRead"/> and <see cref="UsageKind.FieldWrite"/>.</summary>
    public string? FieldName { get; init; }

    /// <summary>Event name for <see cref="UsageKind.EventSubscribe"/>, <see cref="UsageKind.EventUnsubscribe"/>, and <see cref="UsageKind.EventCall"/>.</summary>
    public string? EventName { get; init; }
}
