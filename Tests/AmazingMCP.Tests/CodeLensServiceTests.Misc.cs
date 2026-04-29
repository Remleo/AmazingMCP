using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class CodeLensServiceTests
{
    // ── Scope (containing type) ───────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_ContainingType_AppearsWithScopePrefix()
    {
        // arrange: lines 28-31 — inside CodeLensTestFixture.GetById body
        // act
        var result = await Act(28, 31);

        // assert: full name of the enclosing class
        result.Should().Contain("scope `TestProject.App.Helpers.CodeLensTestFixture`");
    }

    [Test]
    public async Task AnalyzeAsync_ContainingType_AppearsOnce_EvenWithMultipleUsages()
    {
        // arrange: lines 43-54 — multiple accesses inside Add body
        // act
        var result = await Act(43, 54);

        // assert: scope entry appears exactly once
        CountOccurrences(result, "scope `TestProject.App.Helpers.CodeLensTestFixture`").Should().Be(1);
    }

    // ── Flat sorted output ────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_Output_HasNoSectionHeaders()
    {
        // arrange: any range with content
        // act
        var result = await Act(28, 31);

        // assert: no markdown section headers
        result.Should().NotContain("## Variables");
        result.Should().NotContain("## Fields");
        result.Should().NotContain("## Calls");
        result.Should().NotContain("## Definitions");
    }

    [Test]
    public async Task AnalyzeAsync_Output_SortedBySourceLine()
    {
        // arrange: lines 16-18 — field _repository (16), field _notification (17), prop DefaultKind (18)
        // act
        var result = await Act(16, 18);
        var sections = StripSourceSnippet(result);
        var lines = sections.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // assert: _repository before _notification before DefaultKind
        var repoIdx = Array.FindIndex(lines, l => l.Contains("_repository"));
        var notifIdx = Array.FindIndex(lines, l => l.Contains("_notification"));
        var kindIdx = Array.FindIndex(lines, l => l.Contains("DefaultKind"));
        repoIdx.Should().BeLessThan(notifIdx);
        notifIdx.Should().BeLessThan(kindIdx);
    }

    // ── System namespace trimming ─────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_SystemTypes_NamespaceTrimmed()
    {
        // arrange: lines 36-38 — variables use IReadOnlyList<Animal>, List<Animal>
        // act
        var result = await Act(36, 38);

        // assert
        result.Should().NotContain("System.Collections.Generic.IReadOnlyList");
        result.Should().NotContain("System.Collections.Generic.List");
    }

    // ── File not found ────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_FileNotFound_ReturnsFileNotFoundMessage()
    {
        // arrange
        var missingFile = Path.Combine(Path.GetTempPath(), "Missing_" + Guid.NewGuid() + ".cs");

        // act
        var result = await _sut.AnalyzeAsync(CompilationHelper.SolutionPath, missingFile, 1, 5);

        // assert
        result.Should().StartWith("File not found in solution:");
    }

    // ── No results ────────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_RangeWithNoTypes_ReturnsNoAnalysisEntries()
    {
        // arrange: line 26 — comment line, no type information
        // act
        var result = await Act(26, 26);

        // assert: no analysis entries
        result.Should().NotContain("var `");
        result.Should().NotContain("field `");
        result.Should().NotContain("call `");
        result.Should().NotContain("def `");
    }
}
