namespace AmazingMCP.Models;

public sealed class FileStructureItem
{
    /// <summary>
    /// The display/signature string used for wildcard matching.
    /// </summary>
    public required string SymbolString { get; init; }

    public required FileStructureItemKind Kind { get; init; }

    /// <summary>
    /// First line of the item including leading xmldoc/attributes (1-based).
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Last line of the item (1-based, inclusive).
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Total line count from StartLine to EndLine.
    /// </summary>
    public int LineCount => EndLine - StartLine + 1;

    /// <summary>
    /// The line that contains the declaration keyword (class/namespace/etc.), 1-based.
    /// For Namespace and Type: line with 'namespace'/'class'/etc. — without xmldoc.
    /// For Member/Usings: same as StartLine.
    /// </summary>
    public required int DeclarationLine { get; init; }

    /// <summary>
    /// Last line of the declaration header (before the opening brace / body), 1-based.
    /// For Type: last line of "class Foo : Base" before '{'.
    /// For Namespace/Member/Usings: same as DeclarationLine.
    /// </summary>
    public required int DeclarationEndLine { get; init; }
}
