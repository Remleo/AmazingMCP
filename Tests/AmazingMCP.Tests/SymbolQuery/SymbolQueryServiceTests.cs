using AmazingMCP.Configuration;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolQueryServiceTests
{
    SymbolQueryService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        var roslyn = new RoslynSymbolService(CreateWorkspaceProvider(cachedSolution), new WildcardPatternFactory(), CreateTypeProvider(), CompilationHelper.CreateVersionedStrategy());
        _sut = new SymbolQueryService(roslyn, Options.Create(new SymbolOptions()));
    }

    async Task<string> ActTypes(params string[] typeQuery) =>
        await _sut.QueryAsync(CompilationHelper.SolutionPath, typeQuery, typeExclude: null, memberQuery: null);

    async Task<string> ActMembers(params string[] memberQuery) =>
        await _sut.QueryAsync(CompilationHelper.SolutionPath, typeQuery: null, typeExclude: null, memberQuery);

    // ── Not found ─────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_NoMatch_ReturnsNotFoundMessage()
    {
        var result = await ActTypes("NonExistentXyzType");
        result.Should().Contain("No types or members matching");
    }

    // ── Exact vs partial split ────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ExactMatch_NoPartialSection()
    {
        // "AnimalKind" matches exactly one type — no partial section expected
        var result = await ActTypes("AnimalKind");
        result.Should().NotContain("partial match");
    }

    [Test]
    public async Task QueryAsync_PartialMatches_ShowsPartialSection()
    {
        // "IAnimal" matches IAnimalService, IAnimalRepository etc. — all partial
        var result = await ActTypes("IAnimal");
        result.Should().Contain("partial match");
    }

    [Test]
    public async Task QueryAsync_ExactAndPartialMatches_BothPresent()
    {
        // "AnimalKind" is an exact match; "AnimalKindExtensions" etc. would be partial
        var result = await ActTypes("AnimalKind");
        result.Should().Contain("[Enum] TestProject.Core.Models.AnimalKind");
    }

    // ── Wildcard — flat output, no split ─────────────────────────────────────

    [Test]
    public async Task QueryAsync_WildcardQuery_NoPartialSection()
    {
        var result = await ActTypes("*Animal*");
        result.Should().NotContain("partial match");
    }

    [Test]
    public async Task QueryAsync_WildcardQuery_ContainsMultipleResults()
    {
        var result = await ActTypes("*Animal*");
        result.Should().Contain("IAnimalService");
        result.Should().Contain("IAnimalRepository");
    }

    // ── Member grouping ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_MemberSearch_GroupedUnderDeclaringType()
    {
        var result = await ActMembers("FindById");
        // declaring type header appears before the member
        result.Should().Contain("IAnimalRepository");
        result.Should().MatchRegex(@"\[Methods\]");
    }

    [Test]
    public async Task QueryAsync_MemberSearch_ShowsKindLabel()
    {
        var result = await ActMembers("GetById");
        result.Should().Contain("[Methods]");
    }

    // ── Truncation ────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ManyResults_TruncatesOutput()
    {
        var sut = new SymbolQueryService(
            new RoslynSymbolService(CreateWorkspaceProvider(await CompilationHelper.GetSharedSolutionAsync()), new WildcardPatternFactory(), CreateTypeProvider(), CompilationHelper.CreateVersionedStrategy()),
            Options.Create(new SymbolOptions { QueryOutputLineLimit = 5 }));

        var result = await sut.QueryAsync(CompilationHelper.SolutionPath, typeQuery: ["*Animal*"], typeExclude: null, memberQuery: null);
        result.Should().Contain("Output truncated");
    }

    [Test]
    public async Task QueryAsync_FewResults_NotTruncated()
    {
        var result = await ActTypes("AnimalKind");
        result.Should().NotContain("Output truncated");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_NoTypeOrMemberQuery_ReturnsValidationMessage()
    {
        // act
        var result = await _sut.QueryAsync(CompilationHelper.SolutionPath, typeQuery: null, typeExclude: null, memberQuery: null);

        // assert
        result.Should().Contain("At least one of typeQuery or memberQuery");
    }

    // ── Type + member query ───────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ExactTypeQueryWithWildcardMemberQuery_NoPartialSection()
    {
        // act — members are judged by memberQuery, which is a wildcard here
        var result = await _sut.QueryAsync(
            CompilationHelper.SolutionPath, typeQuery: ["IAnimalRepository"], typeExclude: null, memberQuery: ["*"]);

        // assert
        result.Should().Contain("FindById");
        result.Should().NotContain("partial match");
    }
}
