using System.Text;
using AmazingMCP.Models;
using AmazingMCP.Models.FileAnalysis;
using AmazingMCP.Models.UsageQuery;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Groups <see cref="UsageMatch"/> results by Type+FilePath, merges all section ranges
/// globally per file, and formats the output as a single csharp block per file.
/// </summary>
public class UsageResultFormatter : IUsageResultFormatter
{
    public string Format(IReadOnlyList<UsageMatch> matches, bool truncated = false)
    {
        if (matches.Count == 0)
            return
                "No usages found.\n\n" +
                "If you expected results, make sure the type name is fully qualified (includes namespace). " +
                "Example: `MyApp.Core.IRequestStream`, not just `IRequestStream`.\n\n" +
                "For closed generics, all type arguments must also be fully qualified: " +
                "`System.Collections.Generic.List<MyApp.Core.Animal>`.\n\n" +
                "For open generics, argument names must match the declaration: " +
                "`MyApp.Persistance.IRepository<TKey, TValue>`.\n\n" +
                "To find the correct full name:\n" +
                "- Use `query_symbol` to locate the type by name and see its fully qualified form.\n" +
                "- Use `code_lens` on any line where the type appears — it shows the full name of every type in the span.";

        var sb = new StringBuilder();

        var byTypeFile = matches
            .GroupBy(m => (m.Scope.TypeName, m.Scope.FilePath))
            .OrderBy(g => g.Key.TypeName)
            .ThenBy(g => g.Key.FilePath);

        foreach (var typeFileGroup in byTypeFile)
        {
            var (typeName, filePath) = typeFileGroup.Key;
            sb.AppendLine($"## {typeName}");
            sb.AppendLine();

            // Synthetic matches (third-party types with no source file)
            var syntheticMatches = typeFileGroup
                .Where(m => m.Scope.Section is null && m.Scope.SyntheticDeclaration is not null)
                .ToList();

            if (syntheticMatches.Count > 0)
            {
                sb.AppendLine("```csharp");
                sb.AppendLine(syntheticMatches[0].Scope.SyntheticDeclaration);
                sb.AppendLine("```");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"file: {filePath}");
            sb.AppendLine();

            var sourceLines = TryReadLines(filePath);

            var items = typeFileGroup
                .Where(m => m.Scope.Section is not null)
                .Select(m => new MatchItem(
                    new LineRange(m.Scope.Section!.StartLine, m.Scope.Section.EndLine),
                    m.Scope.MethodDefinitionRange))
                .ToList();

            // Merge section ranges + method definition ranges so definitions are never duplicated
            var allRanges = items.Select(i => i.Section)
                .Concat(items.Where(i => i.MethodDef.HasValue).Select(i => i.MethodDef!.Value))
                .ToList();
            var mergedBlocks = MergeRanges(allRanges);

            sb.AppendLine("```csharp");

            for (var bi = 0; bi < mergedBlocks.Count; bi++)
            {
                if (bi > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(CutWithIndentOf(sourceLines, mergedBlocks[bi]));
                    sb.AppendLine();
                }

                AppendCodeLines(sb, sourceLines, mergedBlocks[bi]);
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        var result = sb.ToString().TrimEnd();

        if (truncated)
            result += $"\n\n---\n> **Too many results ({matches.Count}+ matches). Output is truncated.** " +
                      "Narrow your query using a more specific predicate or add `scanInclude`/`scanExclude` to limit the scanned types.";

        return result;
    }

    static void AppendCodeLines(StringBuilder sb, string[]? sourceLines, LineRange range)
    {
        var indent = DetectIndent(sourceLines, range);
        var label = range.Count == 1
            ? $"{indent}// line {range.Start} +1"
            : $"{indent}// lines {range.Start} +{range.Count}";

        sb.AppendLine(label);

        if (sourceLines is not null)
        {
            var from = Math.Max(0, range.Start - 1);
            var to   = Math.Min(sourceLines.Length - 1, range.End - 1);
            for (var i = from; i <= to; i++)
                sb.AppendLine(sourceLines[i]);
        }
        else
        {
            sb.AppendLine($"{indent}// (source file not readable)");
        }
    }

    static string CutWithIndentOf(string[]? sourceLines, LineRange nextRange)
    {
        var indent = DetectIndent(sourceLines, nextRange);
        return $"{indent}// ...";
    }

    static string DetectIndent(string[]? sourceLines, LineRange range)
    {
        if (sourceLines is null) return string.Empty;
        var idx = range.Start - 1;
        if (idx < 0 || idx >= sourceLines.Length) return string.Empty;
        return DetectIndentFromLine(sourceLines[idx]);
    }

    static string DetectIndentFromLine(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ') count++;
            else if (ch == '\t') count += 4;
            else break;
        }
        return new string(' ', count);
    }

    static List<LineRange> MergeRanges(List<LineRange> ranges)
    {
        if (ranges.Count == 0) return [];

        var sorted = ranges.OrderBy(r => r.Start).ThenBy(r => r.End).ToList();
        var merged = new List<LineRange>();
        var current = sorted[0];

        for (var i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];
            if (next.Start <= current.End + 1)
                current = current.MergeWith(next);
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    static string[]? TryReadLines(string filePath)
    {
        try { return File.Exists(filePath) ? File.ReadAllLines(filePath) : null; }
        catch { return null; }
    }

    readonly record struct MatchItem(LineRange Section, LineRange? MethodDef);
}
