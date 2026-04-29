using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.CodeLens;

/// <summary>
/// Determines whether a type is "trivial" and should be omitted from code lens output.
/// Trivial = primitives, void, string, CancellationToken, bare Task/ValueTask,
/// and Nullable&lt;T&gt; where T is also trivial.
/// </summary>
public static class CodeLensTypeChecker
{
    // Full CLR names that are always trivial (non-nullable forms).
    static readonly HashSet<string> TrivialFullNames = new(StringComparer.Ordinal)
    {
        "System.String",
        "System.Boolean",
        "System.Byte", "System.SByte",
        "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32",
        "System.Int64", "System.UInt64",
        "System.Single", "System.Double", "System.Decimal",
        "System.Char",
        "System.Object",
        "System.Void",
        "System.Threading.CancellationToken",
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.ValueTask",
    };

    // Short names (after System.* trimming) that are trivial.
    static readonly HashSet<string> TrivialShortNames = new(StringComparer.Ordinal)
    {
        "string", "bool",
        "byte", "sbyte", "short", "ushort",
        "int", "uint", "long", "ulong",
        "float", "double", "decimal",
        "char", "object", "void",
        // Capitalised aliases
        "String", "Boolean",
        "Byte", "SByte", "Int16", "UInt16",
        "Int32", "UInt32", "Int64", "UInt64",
        "Single", "Double", "Decimal",
        "Char", "Object", "Void",
        "CancellationToken",
        "Task", "ValueTask",
    };

    /// <summary>
    /// Returns true if the Roslyn type symbol is trivial and should be omitted.
    /// Handles Nullable&lt;T&gt; by unwrapping and checking the inner type.
    /// </summary>
    public static bool IsTrivial(ITypeSymbol type)
    {
        // Unwrap Nullable<T>
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return IsTrivial(nullable.TypeArguments[0]);

        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);

        return IsTrivialByName(fullName);
    }

    /// <summary>
    /// Returns true if the display name (after System.* trimming) represents a trivial type.
    /// Also handles Task&lt;T&gt; / ValueTask&lt;T&gt; where T is trivial.
    /// </summary>
    public static bool IsTrivialDisplayName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return true;

        // Nullable suffix from display string (e.g. "int?")
        if (name.EndsWith('?'))
            return IsTrivialDisplayName(name[..^1]);

        if (TrivialShortNames.Contains(name)) return true;
        if (TrivialFullNames.Contains(name)) return true;

        // Task<T> / ValueTask<T> — trivial only when T is trivial
        if (TryUnwrapGeneric(name, "Task", out var taskInner))
            return IsTrivialDisplayName(taskInner);

        if (TryUnwrapGeneric(name, "ValueTask", out var vtInner))
            return IsTrivialDisplayName(vtInner);

        return false;
    }

    // ── private helpers ───────────────────────────────────────────────────

    static bool IsTrivialByName(string fullName)
    {
        if (TrivialFullNames.Contains(fullName)) return true;

        // Nullable<T> in CLR form: "System.Nullable<System.Int32>"
        if (TryUnwrapGeneric(fullName, "System.Nullable", out var nullableInner))
            return IsTrivialByName(nullableInner);

        // Task<T> / ValueTask<T>
        if (TryUnwrapGeneric(fullName, "System.Threading.Tasks.Task", out var taskInner))
            return IsTrivialByName(taskInner);

        if (TryUnwrapGeneric(fullName, "System.Threading.Tasks.ValueTask", out var vtInner))
            return IsTrivialByName(vtInner);

        return false;
    }

    static bool TryUnwrapGeneric(string name, string prefix, out string inner)
    {
        var pattern = prefix + "<";
        if (name.StartsWith(pattern, StringComparison.Ordinal) && name.EndsWith('>'))
        {
            inner = name[pattern.Length..^1];
            return true;
        }
        inner = string.Empty;
        return false;
    }
}
