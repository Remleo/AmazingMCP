using System.Text;
using AmazingMCP.Models;

namespace AmazingMCP.Services;

/// <summary>
/// Groups <see cref="UsageMatch"/> results by Type+FilePath, merges all section ranges
/// globally per file, and formats the output as a single csharp block per file.
/// </summary>
public static class UsageResultFormatter
{
    public static string Format(IReadOnlyList<UsageMatch> matches, bool truncated = false)
    {
        if (matches.Count == 0)
            return "No usages found matching the predicate.";

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
            sb.AppendLine($"file: {filePath}");
            sb.AppendLine();

            var sourceLines = TryReadLines(filePath);

            // Collect all items, sort by section start
            var items = typeFileGroup
                .Select(m => new MatchItem(
                    new LineRange(m.Scope.Section.StartLine, m.Scope.Section.EndLine),
                    m.Scope.MethodName,
                    m.Scope.MethodDefinitionRange,
                    m.Scope.MethodFullRange))
                .OrderBy(i => i.Section.Start)
                .ToList();

            // Merge all section ranges globally
            var mergedBlocks = MergeRanges(items.Select(i => i.Section).ToList());

            sb.AppendLine("```csharp");

            var shownDefinitions = new HashSet<(string, int)>(); // (MethodName, DefStart)

            for (var bi = 0; bi < mergedBlocks.Count; bi++)
            {
                if (bi > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(CutWithIndentOf(sourceLines, mergedBlocks[bi]));
                    sb.AppendLine();
                }

                AppendBlock(sb, mergedBlocks[bi], items, sourceLines, shownDefinitions);
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

    // ── Block rendering ───────────────────────────────────────────────────────

    static void AppendBlock(
        StringBuilder sb,
        LineRange block,
        List<MatchItem> allItems,
        string[]? sourceLines,
        HashSet<(string, int)> shownDefinitions)
    {
        var methodHeaders = allItems
            .Where(i => i.Section.Overlaps(block) && i.MethodName is not null && i.MethodDef.HasValue)
            .Select(i => (i.MethodName!, i.MethodDef!.Value, i.MethodFull))
            .DistinctBy(x => (x.Item1, x.Item2.Start))
            .OrderBy(x => x.Item2.Start)
            .Where(x =>
            {
                var trimmed = TrimDefinitionRange(x.Item2, sourceLines);
                return !block.Contains(trimmed.Start);
            })
            .ToList();

        var firstChunk = true;

        foreach (var (name, defRange, fullRange) in methodHeaders)
        {
            var key = (name, defRange.Start);
            if (!shownDefinitions.Add(key)) continue; // already shown for a previous block

            var trimmed = TrimDefinitionRange(defRange, sourceLines);

            if (!firstChunk) { sb.AppendLine(); sb.AppendLine(CutWithIndentOf(sourceLines, trimmed)); sb.AppendLine(); }
            firstChunk = false;

            // Use the full method range for the annotation so the reader knows the total extent.
            var annotationRange = fullRange ?? defRange;
            AppendCodeLines(sb, sourceLines, trimmed, annotationRange);
            sb.AppendLine();
            sb.AppendLine(CutWithIndentOf(sourceLines, block));
            sb.AppendLine();
        }

        AppendCodeLines(sb, sourceLines, block);
    }

    // ── Code lines ────────────────────────────────────────────────────────────

    static void AppendCodeLines(StringBuilder sb, string[]? sourceLines, LineRange range, LineRange? annotationRange = null)
    {
        var display = annotationRange ?? range;
        var indent = DetectIndent(sourceLines, range);
        var label = display.Count == 1
            ? $"{indent}// line {display.Start} +1"
            : $"{indent}// lines {display.Start} +{display.Count}";

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

    // ── Cut separator ─────────────────────────────────────────────────────────

    static string CutWithIndentOf(string[]? sourceLines, LineRange nextRange)
    {
        var indent = DetectIndent(sourceLines, nextRange);
        return $"{indent}// ...";
    }

    // ── Definition range trimming ─────────────────────────────────────────────

    static LineRange TrimDefinitionRange(LineRange range, string[]? sourceLines)
    {
        if (sourceLines is null) return range;

        var start = range.Start;
        while (start < range.End)
        {
            var line = sourceLines[start - 1].TrimStart();
            if (line.StartsWith('[') || line.StartsWith("///") || line.StartsWith("//"))
                start++;
            else
                break;
        }

        return new LineRange(start, range.End);
    }

    // ── Indent detection ──────────────────────────────────────────────────────

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

    // ── Range merging ─────────────────────────────────────────────────────────

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

    // ── File reading ──────────────────────────────────────────────────────────

    static string[]? TryReadLines(string filePath)
    {
        try { return File.Exists(filePath) ? File.ReadAllLines(filePath) : null; }
        catch { return null; }
    }

    // ── Internal types ────────────────────────────────────────────────────────

    readonly record struct MatchItem(LineRange Section, string? MethodName, LineRange? MethodDef, LineRange? MethodFull);
}
