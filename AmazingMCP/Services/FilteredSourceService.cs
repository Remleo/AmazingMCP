using AmazingMCP.Models;

namespace AmazingMCP.Services;

public class FilteredSourceService(
    IFileStructureService fileStructure,
    IWildcardPatternFactory wildcardFactory,
    IFileReader fileReader) : IFilteredSourceService
{
    const string CutMarker = "// << ... cut ... >>";
    const int SmallTypeThreshold = 50;

    public string GetFilteredSource(string filePath, string[]? filters)
    {
        filePath = Path.GetFullPath(filePath);

        if (!fileReader.Exists(filePath))
            return $"File not found: {filePath}";

        if (filters is not { Length: > 0 })
        {
            if (fileReader.GetLength(filePath) > 30_000)
                return $"File is too large ({fileReader.GetLength(filePath):N0} chars) to return without filters. " +
                       "Use wildcard `filters` to select specific members, or call `read_cs_file_digest` to see the compact outline.";

            return fileReader.ReadAllText(filePath);
        }

        var sourceLines = fileReader.ReadAllLines(filePath);
        var items = fileStructure.GetItems(filePath);
        var matchers = filters.Select(wildcardFactory.CreateGlob).ToArray();

        // ── 1. collect matched ranges ──────────────────────────────────────────
        var seen = new HashSet<int>();
        var ranges = new List<(int Start, int End)>();

        foreach (var item in items)
        {
            if (item.Kind == FileStructureItemKind.Namespace) continue;
            if (!matchers.Any(m => m.IsMatch(item.SymbolString))) continue;
            if (!seen.Add(item.StartLine)) continue;

            var end = item.Kind == FileStructureItemKind.Type && item.LineCount > SmallTypeThreshold
                ? item.DeclarationEndLine
                : Math.Min(item.EndLine, sourceLines.Length - 1);

            ranges.Add((item.StartLine, end));
        }

        if (ranges.Count == 0)
            return "// << ... cut ... >>\n// No matches found.";

        // ── 2. add container type declarations for all matched ranges ──────────
        foreach (var item in items)
        {
            if (item.Kind != FileStructureItemKind.Type) continue;
            if (ranges.Any(r => r.Start > item.StartLine && r.Start <= item.EndLine))
                ranges.Add((item.StartLine, item.DeclarationEndLine));
        }

        // ── 3. add namespace declarations (always) ────────────────────────────
        foreach (var item in items)
        {
            if (item.Kind == FileStructureItemKind.Namespace)
                ranges.Add((item.StartLine, item.DeclarationEndLine));
        }

        // ── 4. sort + merge + build output ────────────────────────────────────
        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = MergeRanges(ranges);
        return BuildOutput(sourceLines, merged);
    }

    static string[] GetLines(string[] sourceLines, int from, int to)
    {
        from = Math.Max(0, from);
        to = Math.Min(sourceLines.Length - 1, to);
        if (from > to) return [];
        return sourceLines[from..(to + 1)];
    }

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

    static string BuildOutput(string[] sourceLines, List<(int Start, int End)> ranges)
    {
        var output = new List<string>();
        var prevEnd = -1;

        foreach (var (start, end) in ranges)
        {
            AppendGap(output, sourceLines, prevEnd + 1, start - 1);

            for (var line = start; line <= end; line++)
                output.Add(sourceLines[line]);

            prevEnd = end;
        }

        AppendTrailing(output, sourceLines, prevEnd + 1);

        return string.Join(Environment.NewLine, output).TrimEnd();
    }

    static void AppendGap(List<string> output, string[] sourceLines, int from, int to)
    {
        if (from > to) return;

        var gapLines = GetLines(sourceLines, from, to);
        if (gapLines.Sum(l => l.Length) <= CutMarker.Length)
        {
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
        var trailingLines = GetLines(sourceLines, from, sourceLines.Length - 1);
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
