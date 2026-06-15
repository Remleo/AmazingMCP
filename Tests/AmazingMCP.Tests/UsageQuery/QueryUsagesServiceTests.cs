using AmazingMCP.Configuration;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class QueryUsagesServiceTests
{
    IQueryUsagesService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        var usageProvider = new UsageProvider(
            CreateWorkspaceProvider(cachedSolution),
            new WildcardPatternFactory(),
            new InheritanceUsageProvider(
                new InheritanceSearchSymbolResolver(CreateTypeProvider(), CreateVersionedStrategy()),
                new RoslynDerivedTypeService(CreateTypeProvider(), CreateAllInstancesStrategy())),
            Options.Create(new QueryUsagesOptions()));

        _sut = new QueryUsagesService(
            usageProvider,
            new UsageResultFormatter(),
            new RoslynSymbolService(
                CreateWorkspaceProvider(cachedSolution),
                new WildcardPatternFactory(),
                CreateTypeProvider(),
                CreateVersionedStrategy()));
    }

    async Task<string> Act(string typeName, string? predicate = null) =>
        await _sut.QueryAsync(CompilationHelper.SolutionPath, typeName, predicate);

    // ── No results ────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_NoMatch_ReturnsNoUsagesMessage()
    {
        var result = await Act("TestProject.Core.Models.NonExistentType");
        result.Should().Contain("No usages found");
    }

    [Test]
    public async Task QueryAsync_NoMatch_WrongNamespaceButSimpleNameExists_SuggestsCorrectType()
    {
        // arrange — wrong namespace, but a type named "IAnimalRepository" really exists
        var result = await Act("Wrong.Namespace.IAnimalRepository");

        // assert
        result.Should().Contain("No usages found");
        result.Should().Contain("TestProject.Core.Persistence.IAnimalRepository");
    }

    // ── Found results — output structure ─────────────────────────────────────

    [Test]
    public async Task QueryAsync_FoundUsages_ContainsTypeHeader()
    {
        var result = await Act("TestProject.Core.Persistence.IAnimalRepository");
        result.Should().Contain("##");
    }

    [Test]
    public async Task QueryAsync_FoundUsages_ContainsCodeBlock()
    {
        var result = await Act("TestProject.Core.Persistence.IAnimalRepository");
        result.Should().Contain("```csharp");
    }

    [Test]
    public async Task QueryAsync_FoundUsages_ContainsFilePath()
    {
        var result = await Act("TestProject.Core.Persistence.IAnimalRepository");
        result.Should().Contain("file:");
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_InvalidPredicate_ReturnsErrorMessage()
    {
        var result = await Act("TestProject.Core.Persistence.IAnimalRepository", "invalid predicate !!!");
        result.Should().StartWith("Error:");
    }
}
