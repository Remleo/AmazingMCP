using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class UsageProviderTestsScanFilters : UsageProviderTestsBase
{
    // ── ScanInclude / ScanExclude ─────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ScanInclude_ExcludesNonMatchingContainingTypes()
    {
        // act — restrict scan to UsageQueryTestFixture only
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — all matches are found inside the filtered containing type
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Scope.TypeName.Should().Be("TestProject.App.Services.UsageQueryTestFixture"));
    }

    [Test]
    public async Task QueryAsync_ScanInclude_WildcardMatchesMultipleContainingTypes()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            scanInclude: ["TestProject.App.Services.*"]);

        // assert — all matches found inside App.Services types
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Scope.TypeName.Should().StartWith("TestProject.App.Services."));
    }

    [Test]
    public async Task QueryAsync_ScanInclude_NoMatchingContainingTypes_ReturnsEmpty()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            scanInclude: ["NonExistent.Namespace.*"]);

        // assert
        matches.Should().BeEmpty();
    }

    [Test]
    public async Task QueryAsync_ScanExclude_ExcludesMatchingContainingTypes()
    {
        // arrange — scan all App.Services types but exclude UsageQueryTestFixture specifically
        var allMatches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.*"]);

        // act
        var filteredMatches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.*"],
            scanExclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — excluded type must not appear in results
        filteredMatches.Should().NotContain(m =>
            m.Scope.TypeName == "TestProject.App.Services.UsageQueryTestFixture");

        // and the excluded type was actually present before exclusion (noise check)
        allMatches.Should().Contain(m =>
            m.Scope.TypeName == "TestProject.App.Services.UsageQueryTestFixture");
    }

    [Test]
    public async Task QueryAsync_ScanExclude_WildcardExcludesMultipleTypes()
    {
        // act — exclude all App.Services types via wildcard
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanExclude: ["TestProject.App.Services.*"]);

        // assert — no matches from App.Services namespace
        matches.Should().NotContain(m =>
            m.Scope.TypeName.StartsWith("TestProject.App.Services."));
    }

    [Test]
    public async Task QueryAsync_ScanExclude_NonMatchingPattern_ExcludesNothing()
    {
        // arrange — baseline without exclusion
        var allMatches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // act — exclude a pattern that matches nothing
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"],
            scanExclude: ["NonExistent.Namespace.*"]);

        // assert — results are identical
        matches.Should().HaveSameCount(allMatches);
    }

    [Test]
    public async Task QueryAsync_ScanExclude_TakesPrecedenceOverScanInclude()
    {
        // act — include and exclude the same type simultaneously
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"],
            scanExclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — exclude wins, no results
        matches.Should().BeEmpty();
    }
}