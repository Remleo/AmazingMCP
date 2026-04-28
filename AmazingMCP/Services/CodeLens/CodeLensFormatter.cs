using AmazingMCP.Models;
using System.Text;

namespace AmazingMCP.Services.CodeLens;

/// <summary>
/// Formats collected <see cref="CodeLensEntry"/> items into a human-readable markdown string.
/// </summary>
public static class CodeLensFormatter
{
    public static string Format(
        List<CodeLensEntry> variables,
        List<CodeLensEntry> calls,
        List<CodeLensEntry> extensions,
        List<CodeLensEntry> constructors,
        List<CodeLensEntry> definitionMethods,
        List<CodeLensEntry> definitionTypes)
    {
        var sb = new StringBuilder();

        AppendSection(sb, "Variables", variables, FormatVariable);
        AppendSection(sb, "Calls", calls, FormatCall);
        AppendSection(sb, "Extensions", extensions, FormatExtension);
        AppendSection(sb, "Constructors", constructors, FormatConstructor);

        var definitions = definitionMethods.Concat(definitionTypes).ToList();
        AppendSection(sb, "Definitions", definitions, FormatDefinition);

        return sb.Length == 0
            ? "No non-trivial types found in the specified range."
            : sb.ToString().TrimEnd();
    }

    private static void AppendSection(
        StringBuilder sb,
        string title,
        List<CodeLensEntry> entries,
        Func<CodeLensEntry, string> formatter)
    {
        if (entries.Count == 0) return;
        if (sb.Length > 0) sb.AppendLine();
        sb.AppendLine($"## {title}");
        foreach (var e in entries)
            sb.AppendLine(formatter(e));
    }

    private static string FormatVariable(CodeLensEntry e)
        => $"var {e.VariableName}: {e.ResolvedType}";

    private static string FormatCall(CodeLensEntry e)
    {
        var placeholder = ArgPlaceholder(e.ArgCount);
        var ret = e.ReturnType != null ? $" → {e.ReturnType}" : string.Empty;
        var detail = ArgDetail(e.ArgTypes);
        return $".{e.MethodName}({placeholder}){ret}{detail}";
    }

    private static string FormatExtension(CodeLensEntry e)
    {
        var placeholder = ArgPlaceholder(e.ArgCount);
        var on = e.ReceiverType != null ? $" on {e.ReceiverType}" : string.Empty;
        var ret = e.ReturnType != null ? $" → {e.ReturnType}" : string.Empty;
        var detail = ArgDetail(e.ArgTypes);
        return $".{e.MethodName}({placeholder}){on}{ret}{detail}";
    }

    private static string FormatConstructor(CodeLensEntry e)
    {
        var placeholder = ArgPlaceholder(e.ArgCount);
        var detail = ArgDetail(e.ArgTypes);
        return $"new {e.TypeFullName}({placeholder}){detail}";
    }

    private static string FormatDefinition(CodeLensEntry e)
    {
        if (e.Kind == CodeLensEntryKind.DefinitionType)
        {
            var bases = e.BaseTypes is { Count: > 0 }
                ? " : " + string.Join(", ", e.BaseTypes)
                : string.Empty;
            return $"def {e.TypeFullName}{bases}";
        }

        var placeholder = ArgPlaceholder(e.ArgCount);
        var ret = e.ReturnType != null ? $" → {e.ReturnType}" : string.Empty;
        var detail = ArgDetail(e.ArgTypes);
        return $"def {e.MethodName}({placeholder}){ret}{detail}";
    }

    /// <summary>
    /// Produces a comma-placeholder: 0 or 1 args → empty, 2 → ",", 3 → ",," etc.
    /// </summary>
    private static string ArgPlaceholder(int count)
        => count <= 1 ? string.Empty : new string(',', count - 1);

    /// <summary>
    /// Produces "  |  args: [0] Type, [1] Type" or empty string.
    /// </summary>
    private static string ArgDetail(IReadOnlyList<string>? argTypes)
    {
        if (argTypes == null || argTypes.Count == 0) return string.Empty;
        var parts = argTypes.Select((t, i) => $"[{i}] {t}");
        return "  |  args: " + string.Join(", ", parts);
    }
}
