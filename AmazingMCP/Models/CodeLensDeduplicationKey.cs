namespace AmazingMCP.Models;

/// <summary>
/// Deduplication keys per CodeLensEntryKind.
/// Each record uses only the fields relevant for that kind.
/// </summary>

public sealed record VariableKey(string Name, string TypeFullName);

public sealed record CallKey(string MethodName);

public sealed record ExtensionKey(string MethodName);

public sealed record ConstructorKey(string TypeFullName);

/// <summary>Kind is included so a method and a class with the same name are not deduplicated against each other.</summary>
public sealed record DefinitionKey(string Name, CodeLensEntryKind Kind);
