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

    /// <summary>Coarse classification: whether this result is a type or a member.</summary>
    public required KindGroup KindGroup { get; init; }

    public required string? ContainingAssembly { get; init; }
    public required string? SourceFilePath { get; init; }
    public required int? DefinitionLine { get; init; }

    /// <summary>All source file paths for this type (multiple entries for partial types). Empty for assembly-only types.</summary>
    public IReadOnlyList<string> SourceFilePaths { get; init; } = [];

    /// <summary>Set for member results; null for type results.</summary>
    public SymbolResult? DeclaringType { get; init; }

    /// <summary>All known NuGet versions for this type across solution compilations. Empty for source types.</summary>
    public IReadOnlyList<Version?> NuGetVersions { get; init; } = [];
}
