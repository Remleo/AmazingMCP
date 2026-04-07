namespace AmazingMCP.Models;

/// <summary>
/// A constructor dependency of an implementation class.
/// </summary>
public record ConstructorDependency(
    /// <summary>
    /// Full type name of the dependency (interface, class, or TOptions for IOptions&lt;T&gt;).
    /// </summary>
    string TypeFullName,
    /// <summary>
    /// True if injected as IOptions&lt;T&gt; — the TypeFullName then points to TOptions.
    /// </summary>
    bool IsOptions,
    /// <summary>
    /// True if injected as IEnumerable&lt;T&gt; (multiple registrations).
    /// </summary>
    bool IsEnumerable);
