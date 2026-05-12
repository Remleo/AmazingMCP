namespace AmazingMCP.Models.CodeLens
{
    /// <summary>
    /// Deduplication keys per CodeLensEntryKind.
    /// Each record uses only the fields relevant for that kind.
    /// </summary>
    public sealed record VariableKey(string Name, string TypeFullName);

    /// <summary>Deduplicates by method name + parameter types + declaring type to allow overloads.</summary>
    public sealed record CallKey(string MethodName, string ParamTypes, string DeclaringType);

    /// <summary>Deduplicates extension methods by name + parameter types + declaring type.</summary>
    public sealed record ExtensionKey(string MethodName, string ParamTypes, string DeclaringType);

    /// <summary>Deduplicates constructors by declaring type + parameter types.</summary>
    public sealed record ConstructorKey(string TypeFullName, string ParamTypes);

    public sealed record FieldKey(string Name, string TypeFullName);

    public sealed record PropertyKey(string Name, string TypeFullName);

    /// <summary>Kind is included so a method and a class with the same name are not deduplicated against each other.</summary>
    public sealed record DefinitionKey(string Name, CodeLensEntryKind Kind);
}