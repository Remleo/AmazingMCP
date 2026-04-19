using System.Text;
using System.Text.RegularExpressions;
using AmazingMCP.Models;

namespace AmazingMCP.Services;

public class FilteredSourceService(FileStructureService fileStructure)
{
    const string CutMarker   = "// << ... cut ... >>";
    const int    MaxTypeLines = 200;

    public string GetFilteredSource(string filePath, string[] filters)
    {
        filePath = Path.GetFullPath(filePath);

        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        if (filters is not { Length: > 0 })
            return "No filters specified.";

        var sourceLines = File.ReadAllLines(filePath);
        var items       = fileStructure.GetItems(filePath);
        var matchers    = filters.Select(WildcardToRegex).ToArray();

        // ── 1. collect matched ranges ──────────────────────────────────────────
        var seen   = new HashSet<int>();
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
        FileStructureItemKind.Type      => item.LineCount <= MaxTypeLines,
        _                               => true   // Usings, Member
    };

    static Regex WildcardToRegex(string pattern)
    {
        var parts   = pattern.Split('*');
        var escaped = string.Join(".*", parts.Select(Regex.Escape));
        return new Regex(escaped, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    // ── range merging ──────────────────────────────────────────────────────────

    static List<(int Start, int End)> MergeRanges(List<(int Start, int End)> sorted)
    {
        var result  = new List<(int, int)>();
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
        // Remove decl ranges already fully covered by a matched range
        var uncoveredDecls = declRanges
            .Where(d => !ranges.Any(r => d.Start >= r.Start && d.End <= r.End))
            .ToList();

        // Combine and re-merge
        var allRanges = ranges
            .Concat(uncoveredDecls)
            .OrderBy(r => r.Start)
            .ToList();

        if (allRanges.Count > 0)
            allRanges = MergeRanges(allRanges);

        // Collect output lines
        var output  = new List<string>();
        var prevEnd = 0;

        foreach (var (start, end) in allRanges)
        {
            if (start > prevEnd + 1)
            {
                // remove trailing blank lines before cut marker
                while (output.Count > 0 && string.IsNullOrWhiteSpace(output[^1]))
                    output.RemoveAt(output.Count - 1);

                if (output.Count > 0) output.Add("");
                output.Add(CutMarker);
                output.Add("");
            }

            for (var line = start; line <= end; line++)
                output.Add(sourceLines[line - 1]);

            prevEnd = end;
        }

        // trailing cut if file has non-empty lines after last range
        var hasTrailingContent = Enumerable.Range(prevEnd + 1, sourceLines.Length - prevEnd)
            .Any(i => i >= 1 && i <= sourceLines.Length && !string.IsNullOrWhiteSpace(sourceLines[i - 1]));

        if (hasTrailingContent)
        {
            while (output.Count > 0 && string.IsNullOrWhiteSpace(output[^1]))
                output.RemoveAt(output.Count - 1);
            output.Add("");
            output.Add(CutMarker);
        }

        return string.Join(Environment.NewLine, output).TrimEnd();
    }
}
