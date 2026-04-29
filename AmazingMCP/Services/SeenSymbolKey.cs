namespace AmazingMCP.Services;

/// <summary>Deduplication key for symbols collected during a query.</summary>
internal record SeenSymbolKey(
    string? ContainingType,
    string Symbol,
    string Assembly)
{
    public static SeenSymbolKey ForType(string typeDisplayName, string assembly) =>
        new(ContainingType: null, Symbol: typeDisplayName, Assembly: assembly);

    public static SeenSymbolKey ForMember(string containingTypeDisplayName, string memberSignature, string assembly) =>
        new(ContainingType: containingTypeDisplayName, Symbol: memberSignature, Assembly: assembly);
}
