namespace AmazingMCP.Models.FileAnalysis;

public sealed class FileStructureItem
{
    public const string UsingsAlias = "usings";
    public const string ConstructorAlias = ".ctor";

    /// <summary>
    /// The display/signature string used for wildcard matching.
    /// </summary>
    //public required string SymbolString { get; init; }

    /// <summary>
    /// Pure member name used for filter matching (e.g. "SendAsync", "MapperConfiguration", "usings").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Additional names that can match this item (e.g. ".ctor" for constructors).
    /// </summary>
    public string[]? NameAliases { get; init; }

    public required FileStructureItemKind Kind { get; init; }

    /// <summary>
    /// First line of the item including leading xmldoc/attributes (0-based).
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Last line of the item (0-based, inclusive).
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Total line count from StartLine to EndLine.
    /// </summary>
    public int LineCount => EndLine - StartLine + 1;

    /// <summary>
    /// Last line of the declaration header (before the opening brace / body), 0-based.
    /// For Type: last line of "class Foo : Base {".
    /// For Namespace/Member/Usings: same as StartLine.
    /// </summary>
    public required int DeclarationEndLine { get; init; }
}
