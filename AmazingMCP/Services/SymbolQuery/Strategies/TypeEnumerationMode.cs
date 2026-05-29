namespace AmazingMCP.Services.SymbolQuery.Strategies;

public enum TypeEnumerationMode
{
    /// <summary>One symbol per unique full name. First encountered wins.</summary>
    Simple,

    /// <summary>One symbol per unique (full name, dll path) pair — all versions, no grouping.</summary>
    AllInstances,

    /// <summary>All versions of each type grouped into a TypeVersionGroup.</summary>
    Versioned,
}
