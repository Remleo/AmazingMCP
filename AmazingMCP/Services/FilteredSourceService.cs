using System.Text.RegularExpressions;
using AmazingMCP.Models;

namespace AmazingMCP.Services;

public class FilteredSourceService(IFileStructureService fileStructure, IWildcardPatternFactory wildcardFactory)
    : IFilteredSourceService
{
    const string CutMarker = "// << ... cut ... >>";
    const int MaxTypeLines = 200;

    public string GetFilteredSource(string filePath, string[]? filters)
    {
        filePath = Path.GetFullPath(filePath);

        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        if (filters is not { Length: > 0 })
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 30_000)
                return $"File is too large ({fileInfo.Length:N0} chars) to return without filters. " +
                       "Use wildcard `filters` to select specific members, or call `read_cs_file_digest` to see the compact outline.";

            return File.ReadAllText(filePath);
        }

        var sourceLines = File.ReadAllLines(filePath);
        var items = fileStructure.GetItems(filePath);
        var matchers = filters.Select(wildcardFactory.CreateGlob).ToArray();

        // ── 1. collect matched ranges ──────────────────────────────────────────
        var seen = new HashSet<int>();
        var ranges = new List<(int Start, int End)>();

        foreach (var item in items)
        {
            if (!IsMatchable(item)) continue;
            if (!matchers.Any(r => r.IsMatch(item.SymbolString))) continue;
            if (!seen.Add(item.StartLine)) continue;

            var end = Math.Min(item.EndLine, sourceLines.Length);
            ranges.Add((item.StartLine, end));
        }

        if (ranges.Count == 0)
            return "// << ... cut ... >>\n// No matches found.";

        // ── 2. collect always-visible declaration ranges (namespace + type header) ──
        //    For types: DeclarationLine..DeclarationEndLine (header before '{')
        //    For namespaces: just DeclarationLine (single line)
        var declRanges = new List<(int Start, int End)>();
        foreach (var item in items)
        {
            if (item.Kind is FileStructureItemKind.Namespace or FileStructureItemKind.Type)
                declRanges.Add((item.DeclarationLine, item.DeclarationEndLine));
        }

        // ── 3. sort + merge matched ranges ────────────────────────────────────
        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = MergeRanges(ranges);

        // ── 4. build output ───────────────────────────────────────────────────
        return BuildOutput(sourceLines, merged, declRanges);
    }

    // ── matching ───────────────────────────────────────────────────────────────

    static bool IsMatchable(FileStructureItem item) => item.Kind switch
    {
        FileStructureItemKind.Namespace => false,
        FileStructureItemKind.Type => item.LineCount <= MaxTypeLines,
        _ => true // Usings, Member
    };

    static string[] GetLines(string[] sourceLines, int from, int to)
    {
        from = Math.Max(1, from);
        to = Math.Min(sourceLines.Length, to);
        if (from > to) return [ ];
        return sourceLines[(from - 1)..to];
    }

    // ── range merging ──────────────────────────────────────────────────────────

    static List<(int Start, int End)> MergeRanges(List<(int Start, int End)> sorted)
    {
        var result = new List<(int, int)>();
        var current = sorted[0];

        for (var i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];
            if (next.Start <= current.End + 1)
                current = (current.Start, Math.Max(current.End, next.End));
            else
            {
                result.Add(current);
                current = next;
            }
        }

        result.Add(current);
        return result;
    }

    // ── output assembly ────────────────────────────────────────────────────────

    static string BuildOutput(
        string[] sourceLines,
        List<(int Start, int End)> ranges,
        List<(int Start, int End)> declRanges)
    {
        var allRanges = MergeWithDeclarations(ranges, declRanges);

        var output = new List<string>();
        var prevEnd = 0;

        foreach (var (start, end) in allRanges)
        {
            AppendGap(output, sourceLines, prevEnd + 1, start - 1);

            for (var line = start; line <= end; line++)
                output.Add(sourceLines[line - 1]);

            prevEnd = end;
        }

        AppendTrailing(output, sourceLines, prevEnd + 1);

        return string.Join(Environment.NewLine, output).TrimEnd();
    }

    static List<(int Start, int End)> MergeWithDeclarations(
        List<(int Start, int End)> ranges,
        List<(int Start, int End)> declRanges)
    {
        var uncoveredDecls = declRanges
            .Where(d => !ranges.Any(r => d.Start >= r.Start && d.End <= r.End));

        var allRanges = ranges
            .Concat(uncoveredDecls)
            .OrderBy(r => r.Start)
            .ToList();

        return allRanges.Count > 0 ? MergeRanges(allRanges) : allRanges;
    }

    static void AppendGap(List<string> output, string[] sourceLines, int from, int to)
    {
        if (from > to) return;

        var gapLines = GetLines(sourceLines, from, to);
        if (gapLines.Sum(l => l.Length) <= CutMarker.Length)
        {
            // Gap is shorter than the marker itself — emit original lines
            output.AddRange(gapLines);
        }
        else
        {
            TrimTrailingBlanks(output);
            if (output.Count > 0) output.Add("");
            output.Add(CutMarker);
            output.Add("");
        }
    }

    static void AppendTrailing(List<string> output, string[] sourceLines, int from)
    {
        var trailingLines = GetLines(sourceLines, from, sourceLines.Length);
        if (trailingLines.All(l => string.IsNullOrWhiteSpace(l))) return;

        if (trailingLines.Sum(l => l.Length) <= CutMarker.Length)
        {
            output.AddRange(trailingLines);
        }
        else
        {
            TrimTrailingBlanks(output);
            output.Add("");
            output.Add(CutMarker);
        }
    }

    static void TrimTrailingBlanks(List<string> output)
    {
        while (output.Count > 0 && string.IsNullOrWhiteSpace(output[^1]))
            output.RemoveAt(output.Count - 1);
    }
}