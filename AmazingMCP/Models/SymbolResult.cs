namespace AmazingMCP.Models;

public record SymbolResult
{
    /// <summary>
    /// Simple name without generic parameters.
    /// For types: the type name. For members: the member name only (no return type, no parameters).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// For types: fully-qualified display name (e.g. <c>Foo.Bar&lt;T&gt;</c>).
    /// For members: full signature without accessibility modifiers (e.g. <c>void DoWork(int x)</c>).
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>Type kind (Class, Interface, Enum, …) or member kind (Method, Property).</summary>
    public required string Kind { get; init; }

    public required string? ContainingAssembly { get; init; }
    public required string? SourceFilePath { get; init; }
    public required int? DefinitionLine { get; init; }

    /// <summary>Set for member results; null for type results.</summary>
    public SymbolResult? DeclaringType { get; init; }
}
