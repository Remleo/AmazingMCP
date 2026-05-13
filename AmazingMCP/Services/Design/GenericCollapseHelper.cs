using AmazingMCP.Models;

namespace AmazingMCP.Services.Design;

/// <summary>
/// Helpers for collapsing closed generic abstractions into their open generic counterparts
/// when both appear in the same result set.
/// </summary>
internal static class GenericCollapseHelper
{
    /// <summary>
    /// Builds a reverse index: openGenericFullName → [closedGenericFullNames] from the map.
    /// O(n) build, O(1) lookup.
    /// </summary>
    public static IReadOnlyDictionary<string, List<string>> BuildOpenToClosedIndex(
        IReadOnlyDictionary<string, string>? closedToOpenMap)
    {
        if (closedToOpenMap is null || closedToOpenMap.Count == 0)
            return new Dictionary<string, List<string>>();

        var result = new Dictionary<string, List<string>>(closedToOpenMap.Count);
        foreach (var (closed, open) in closedToOpenMap)
        {
            if (!result.TryGetValue(open, out var list))
                result[open] = list = [];
            list.Add(closed);
        }
        return result;
    }

    /// <summary>
    /// Given a list of matched abstraction names, collapses closed generics into their open generic
    /// when the open generic is also in the list.
    ///
    /// When an open generic IS in the matched list:
    ///   - ALL its closed generics (from ClosedToOpenGenericMap, not just matched ones) are collapsed into it
    ///   - Closed generics that were also matched are removed from the output list
    ///
    /// When an open generic is NOT in the matched list:
    ///   - Its closed generics are shown as-is (no collapsing)
    ///
    /// Returns:
    /// - finalNames: ordered list of names to render
    /// - collapsedCloseds: for each open generic name → ALL closed generic names collapsed into it
    /// </summary>
    public static (List<string> FinalNames, IReadOnlyDictionary<string, List<string>> CollapsedCloseds)
        Collapse(
            IReadOnlyList<string> matchedNames,
            IReadOnlyDictionary<string, string>? closedToOpenMap,
            IReadOnlyDictionary<string, List<string>> openToClosedIndex)
    {
        if (closedToOpenMap is null || closedToOpenMap.Count == 0)
            return (matchedNames.ToList(), new Dictionary<string, List<string>>());

        var matchedSet = new HashSet<string>(matchedNames);
        var collapsedCloseds = new Dictionary<string, List<string>>();
        var skipped = new HashSet<string>();

        // For each matched open generic: collapse ALL its closeds (not just matched ones)
        foreach (var name in matchedNames)
        {
            if (!openToClosedIndex.TryGetValue(name, out var allCloseds)) continue;

            // This is an open generic that matched — collapse all its closeds
            collapsedCloseds[name] = allCloseds;

            // Skip any of its closeds that also happened to be in the matched list
            foreach (var closed in allCloseds)
                if (matchedSet.Contains(closed))
                    skipped.Add(closed);
        }

        var finalNames = matchedNames.Where(n => !skipped.Contains(n)).ToList();
        return (finalNames, collapsedCloseds);
    }

    /// <summary>
    /// Returns all abstraction names to query for "Used by" — for an open generic this includes
    /// the open generic itself plus all its collapsed closed generics.
    /// </summary>
    public static IEnumerable<string> GetEffectiveAbstractionNames(
        string abstractionName,
        IReadOnlyDictionary<string, List<string>> collapsedCloseds)
    {
        yield return abstractionName;
        if (collapsedCloseds.TryGetValue(abstractionName, out var closeds))
            foreach (var c in closeds)
                yield return c;
    }
}
