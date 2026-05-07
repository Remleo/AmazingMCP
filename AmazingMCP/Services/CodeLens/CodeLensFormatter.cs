using AmazingMCP.Models;
using System.Text;

namespace AmazingMCP.Services.CodeLens;

/// <summary>
/// Formats collected <see cref="CodeLensEntry"/> items into a flat sorted markdown string.
/// All entries are sorted by <see cref="CodeLensEntry.SourceLine"/> and rendered with a keyword prefix
/// followed by a backtick-wrapped C#-style signature.
/// </summary>
public static class CodeLensFormatter
{
    public static string Format(
        List<CodeLensEntry> variables,
        List<CodeLensEntry> calls,
        List<CodeLensEntry> extensions,
        List<CodeLensEntry> constructors,
        List<CodeLensEntry> fields,
        List<CodeLensEntry> properties,
        List<CodeLensEntry> definitionMethods,
        List<CodeLensEntry> definitionTypes,
        List<CodeLensEntry> containingTypes,
        string sourceSnippet)
    {
        var all = variables
            .Concat(calls)
            .Concat(extensions)
            .Concat(constructors)
            .Concat(fields)
            .Concat(properties)
            .Concat(definitionMethods)
            .Concat(definitionTypes)
            .Concat(containingTypes)
            .OrderBy(e => e.SourceLine)
            .ToList();

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(sourceSnippet))
        {
            sb.AppendLine("```csharp");
            sb.AppendLine(sourceSnippet);
            sb.AppendLine("```");
        }

        if (all.Count > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine("```");
            foreach (var e in all)
                sb.AppendLine(FormatEntry(e));
            sb.AppendLine("```");
        }

        if (sb.Length == 0)
            return string.IsNullOrWhiteSpace(sourceSnippet)
                ? "No non-trivial types found in the specified range."
                : $"```csharp\n{sourceSnippet}\n```\n\nNo non-trivial types found in the specified range.";

        return sb.ToString().TrimEnd();
    }

    static string FormatEntry(CodeLensEntry e) => e.Kind switch
    {
        CodeLensEntryKind.Variable        => FormatVariable(e),
        CodeLensEntryKind.Field           => FormatField(e),
        CodeLensEntryKind.Property        => FormatProperty(e),
        CodeLensEntryKind.DefinitionField => FormatField(e),
        CodeLensEntryKind.DefinitionProperty => FormatProperty(e),
        CodeLensEntryKind.Call            => FormatCall(e),
        CodeLensEntryKind.Extension       => FormatExtension(e),
        CodeLensEntryKind.Constructor     => FormatConstructor(e),
        CodeLensEntryKind.DefinitionMethod => FormatDefinitionMethod(e),
        CodeLensEntryKind.DefinitionType  => FormatDefinitionType(e),
        CodeLensEntryKind.ContainingType  => FormatContainingType(e),
        _                                 => string.Empty,
    };

    // var `Type name`
    static string FormatVariable(CodeLensEntry e)
        => $"var `{e.ResolvedType} {e.VariableName}`";

    // field `Type name`
    static string FormatField(CodeLensEntry e)
        => $"field `{e.ResolvedType} {e.VariableName}`";

    // prop `Type name`
    static string FormatProperty(CodeLensEntry e)
        => $"prop `{e.ResolvedType} {e.VariableName}`";

    // call `ReturnType MethodName(Type name, ...)` from `DeclaringType`
    static string FormatCall(CodeLensEntry e)
    {
        var ret = e.ReturnType != null ? $"{e.ReturnType} " : string.Empty;
        var paramList = BuildParamList(e.ArgTypes, e.ArgNames);
        var sig = $"{ret}{e.MethodName}({paramList})";
        var from = e.DeclaringType != null ? $" from `{e.DeclaringType}`" : string.Empty;
        return $"call `{sig}`{from}";
    }

    // call ext `ReturnType MethodName(this Type name, Type name, ...)` from `DeclaringType`
    static string FormatExtension(CodeLensEntry e)
    {
        var ret = e.ReturnType != null ? $"{e.ReturnType} " : string.Empty;
        var thisParam = e.ReceiverType != null && e.ReceiverParamName != null
            ? $"this {e.ReceiverType} {e.ReceiverParamName}"
            : e.ReceiverType != null ? $"this {e.ReceiverType} _" : "this _";
        var restParams = BuildParamList(e.ArgTypes, e.ArgNames);
        var paramList = restParams.Length > 0 ? $"{thisParam}, {restParams}" : thisParam;
        var sig = $"{ret}{e.MethodName}({paramList})";
        var from = e.DeclaringType != null ? $" from `{e.DeclaringType}`" : string.Empty;
        return $"call ext `{sig}`{from}";
    }

    // new `ShortTypeName(Type name, ...)`
    static string FormatConstructor(CodeLensEntry e)
    {
        var paramList = BuildParamList(e.ArgTypes, e.ArgNames);
        return $"new `{e.TypeShortName}({paramList})`";
    }

    // def `ReturnType MethodName(Type name, ...)`
    static string FormatDefinitionMethod(CodeLensEntry e)
    {
        var ret = e.ReturnType != null ? $"{e.ReturnType} " : string.Empty;
        var paramList = BuildParamList(e.ArgTypes, e.ArgNames);
        // ctor uses short class name
        var name = e.MethodName == ".ctor" ? e.TypeShortName ?? ".ctor" : e.MethodName!;
        var prefix = e.MethodName == ".ctor" ? "ctor" : "def";
        return $"{prefix} `{ret}{name}({paramList})`";
    }

    // def `ShortTypeName(Type name, ...) : BaseType, IInterface`
    static string FormatDefinitionType(CodeLensEntry e)
    {
        var ctorParams = e.ArgCount > 0 ? $"({BuildParamList(e.ArgTypes, e.ArgNames)})" : string.Empty;
        var bases = e.BaseTypes is { Count: > 0 }
            ? " : " + string.Join(", ", e.BaseTypes)
            : string.Empty;
        return $"def `{e.TypeShortName}{ctorParams}{bases}`";
    }

    // scope `FullTypeName`
    static string FormatContainingType(CodeLensEntry e)
        => $"scope `{e.TypeFullName}`";

    /// <summary>
    /// Builds "Type name, Type name, ..." parameter list.
    /// </summary>
    static string BuildParamList(IReadOnlyList<string>? argTypes, IReadOnlyList<string>? argNames)
    {
        if (argTypes == null || argTypes.Count == 0) return string.Empty;

        if (argNames != null && argNames.Count == argTypes.Count)
            return string.Join(", ", argTypes.Select((t, i) => $"{t} {argNames[i]}"));

        return string.Join(", ", argTypes);
    }
}
