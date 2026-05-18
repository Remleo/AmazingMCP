using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.CodeLens;

public partial class CodeLensServiceTests
{
    // ── Extensions ────────────────────────────────────────────────────────
    // CodeLensTestFixture line map:
    //   37-39: GetByKind body — .Where(...).ToList()
    //
    // Where and ToList are declared in System.Linq — filtered out by System declaring type rule.

    [Test]
    public async Task AnalyzeAsync_LinqExtensionCalls_NotIncluded_BecauseSystemDeclaringType()
    {
        // arrange: lines 37-39 — Where and ToList from System.Linq
        // act
        var result = await Act(37, 39);

        // assert: no call ext entries for System methods
        var sections = StripSourceSnippet(result);
        sections.Should().NotContain("call ext `");
    }

    [Test]
    public async Task AnalyzeAsync_SystemTypes_NamespaceTrimmed_InVariables()
    {
        // arrange: lines 36-38 — var all: IReadOnlyList<Animal>, var filtered: List<Animal>
        // act
        var result = await Act(36, 38);

        // assert: System.Collections.Generic prefix trimmed
        result.Should().NotContain("System.Collections.Generic.IReadOnlyList");
        result.Should().NotContain("System.Collections.Generic.List");
    }
}
