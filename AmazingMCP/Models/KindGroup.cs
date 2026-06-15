namespace AmazingMCP.Models;

/// <summary>
/// Coarse classification of a <see cref="SymbolResult"/>: a type (class, interface, struct,
/// enum, delegate) or a member (method, property, field, event, enum value).
/// </summary>
public enum KindGroup
{
    Type,
    Member,
}
