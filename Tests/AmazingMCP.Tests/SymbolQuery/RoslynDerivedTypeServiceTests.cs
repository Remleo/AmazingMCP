using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using static AmazingMCP.Tests.Helpers.CompilationHelper;

namespace AmazingMCP.Tests.SymbolQuery;

public class RoslynDerivedTypeServiceTests
{
    CachedSolution _cachedSolution = null!;
    RoslynSymbolService _symbolService = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _symbolService = new RoslynSymbolService(
            CreateWorkspaceProvider(_cachedSolution),
            new WildcardPatternFactory());
    }

    async Task<IReadOnlyList<string>> Act(string fullTypeName)
    {
        var (symbol, _) = await _symbolService.FindExactTypeAsync(_cachedSolution, fullTypeName);
        if (symbol is null) return [];
        return RoslynDerivedTypeService.FindDerivedTypes(_cachedSolution, symbol)
            .Select(t => t.ToDisplayString())
            .ToList();
    }

    // ── Interface: direct implementors ────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_Interface_ReturnsDirectImplementors()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().Contain("TestProject.App.Services.AnimalService");
        result.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
        result.Should().Contain("TestProject.App.Services.TracedAnimalService");
    }

    [Test]
    public async Task FindDerivedTypes_Interface_ReturnsIndirectImplementors()
    {
        // act — TracedServiceA implements IAnimalService and ITracedService
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().Contain("TestProject.App.Services.TracedServiceA");
    }

    [Test]
    public async Task FindDerivedTypes_Interface_DoesNotReturnUnrelatedTypes()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert — ITracedService implementors that don't implement IAnimalService should not appear
        result.Should().NotContain("TestProject.App.Services.MultiRoleService");
    }

    // ── Interface: no implementors ────────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_InterfaceWithNoImplementors_ReturnsEmpty()
    {
        // act — IAnimalValidator has no implementors in the test solution
        var result = await Act("TestProject.Core.Services.IAnimalValidator");

        // assert
        result.Should().BeEmpty();
    }

    // ── Interface: multiple implementors ─────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_InterfaceWithMultipleImplementors_ReturnsAll()
    {
        // act
        var result = await Act("TestProject.App.Services.ITracedService");

        // assert
        result.Should().Contain("TestProject.App.Services.TracedServiceA");
        result.Should().Contain("TestProject.App.Services.TracedServiceB");
    }

    // ── Class: direct subclasses ──────────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_AbstractClass_ReturnsDirectSubclasses()
    {
        // act
        var result = await Act("TestProject.App.Services.AnimalServiceBase");

        // assert
        result.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
    }

    [Test]
    public async Task FindDerivedTypes_AbstractClass_DoesNotReturnUnrelatedClasses()
    {
        // act
        var result = await Act("TestProject.App.Services.AnimalServiceBase");

        // assert
        result.Should().NotContain("TestProject.App.Services.AnimalService");
        result.Should().NotContain("TestProject.App.Services.TracedAnimalService");
    }

    // ── Class: indirect subclasses ────────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_BaseClass_ReturnsIndirectSubclasses()
    {
        // act — ConcreteAnimal extends AnimalBase
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().Contain("TestProject.Core.Models.ConcreteAnimal");
    }

    // ── Class: no subclasses ──────────────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_SealedOrLeafClass_ReturnsEmpty()
    {
        // act — AnimalService has no known subclasses in the test solution
        var result = await Act("TestProject.App.Services.AnimalService");

        // assert
        result.Should().BeEmpty();
    }

    // ── Enum: not applicable ──────────────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_Enum_ReturnsEmpty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().BeEmpty();
    }

    // ── Deduplication ─────────────────────────────────────────────────────────

    [Test]
    public async Task FindDerivedTypes_Interface_NoDuplicatesAcrossProjects()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert — each type appears exactly once
        result.Should().OnlyHaveUniqueItems();
    }
}
