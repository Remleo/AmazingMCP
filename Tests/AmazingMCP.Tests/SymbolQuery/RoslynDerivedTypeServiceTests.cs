using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using Microsoft.CodeAnalysis;
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

    // ── Generic interface: open generic finds closed implementations ─────────

    [Test]
    public async Task FindDerivedTypes_OpenGenericInterface_FindsClosedImplementors()
    {
        // act — IRepository<T> should find AnimalRepository : IRepository<Animal>
        var result = await Act("TestProject.Core.Persistence.IRepository<T>");

        // assert
        result.Should().Contain("TestProject.App.Persistence.AnimalRepository");
    }

    [Test]
    public async Task FindDerivedTypes_OpenGenericInterface_FindsGenericImplementors()
    {
        // act — IRepository<T> should find GenericRepository<T> : IRepository<T>
        var result = await Act("TestProject.Core.Persistence.IRepository<T>");

        // assert
        result.Should().Contain("TestProject.App.Persistence.GenericRepository<T>");
    }

    [Test]
    public async Task FindDerivedTypes_OpenGenericInterface_MultipleTypeParams_FindsImplementors()
    {
        // act — IEntityMapper<TSource, TDestination> should find concrete implementors
        var result = await Act("TestProject.App.Mapping.IEntityMapper<TSource, TDestination>");

        // assert
        result.Should().Contain("TestProject.App.Mapping.AppAnimalMapper");
        result.Should().Contain("TestProject.App.Mapping.AutoMapperAnimalMapper");
        result.Should().Contain("TestProject.App.Mapping.AnimalCreatedEventMapper");
    }

    [Test]
    public async Task FindDerivedTypes_ClosedGenericInterface_FindsExactMatch()
    {
        // arrange — construct closed generic IRepository<Animal> from open generic + Animal type
        var (openSymbol, _) = await _symbolService.FindExactTypeAsync(_cachedSolution, "TestProject.Core.Persistence.IRepository<T>");
        var (animalSymbol, _) = await _symbolService.FindExactTypeAsync(_cachedSolution, "TestProject.Core.Models.Animal");
        Assert.That(openSymbol, Is.Not.Null);
        Assert.That(animalSymbol, Is.Not.Null);

        var closedSymbol = openSymbol!.Construct(animalSymbol!);

        // act
        var result = RoslynDerivedTypeService.FindDerivedTypes(_cachedSolution, closedSymbol)
            .Select(t => t.ToDisplayString())
            .ToList();

        // assert — should find types implementing exactly IRepository<Animal>
        result.Should().Contain("TestProject.App.Persistence.AnimalRepository");
        // GenericRepository<T> implements IRepository<T>, not IRepository<Animal> specifically
        result.Should().NotContain("TestProject.App.Persistence.GenericRepository<T>");
    }

    // ── Generic class: open generic finds closed subclasses ───────────────────

    [Test]
    public async Task FindDerivedTypes_OpenGenericClass_FindsClosedSubclasses()
    {
        // act — RepositoryBase<T> should find AnimalRepositoryV2 : RepositoryBase<Animal>
        var result = await Act("TestProject.Core.Persistence.RepositoryBase<T>");

        // assert
        result.Should().Contain("TestProject.App.Persistence.AnimalRepositoryV2");
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
