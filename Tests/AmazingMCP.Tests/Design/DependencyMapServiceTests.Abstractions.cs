using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.Design;

public partial class DependencyMapServiceTests
{
    #region Abstractions — interface with implementation

    [Test]
    public async Task BuildMapAsync_InterfaceWithImplementation_AppearsInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey("TestProject.Core.Services.IAnimalService");
        result.Abstractions["TestProject.Core.Services.IAnimalService"].Should().BeEquivalentTo(new
        {
            FullName = "TestProject.Core.Services.IAnimalService",
            Namespace = "TestProject.Core.Services",
            IsInterface = true
        }, options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task BuildMapAsync_InterfaceAbstraction_ListsAllImplementations()
    {
        var result = await Act();

        var abstraction = result.Abstractions["TestProject.Core.Services.IAnimalService"];
        abstraction.Implementations.Should().Contain("TestProject.App.Services.AnimalService");
        abstraction.Implementations.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
        abstraction.Implementations.Should().Contain("TestProject.App.Services.MultiInterfaceService");
    }

    #endregion

    #region Abstractions — excluded system interfaces

    [Test]
    public async Task BuildMapAsync_SystemInterfaces_ExcludedFromAbstractions()
    {
        var result = await Act();

        result.Abstractions.Keys.Should().NotContain(k =>
            k.StartsWith("System.") || k.StartsWith("Microsoft.Extensions.Options."));
    }

    #endregion

    #region Standalone class — no interface, no usages → not in map

    [Test]
    public async Task BuildMapAsync_StandaloneClass_NotInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().NotContainKey("TestProject.App.Helpers.StandaloneHelper");
        result.Implementations.Should().NotContainKey("TestProject.App.Helpers.StandaloneHelper");
    }

    #endregion

    #region Abstract classes excluded from implementations

    [Test]
    public async Task BuildMapAsync_AbstractClass_AppearsAsAbstraction()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey("TestProject.App.Services.AnimalServiceBase");
    }

    #endregion

    #region Test project filtering

    [Test]
    public async Task BuildMapAsync_TestProjectTypes_ExcludedFromMap()
    {
        var result = await Act();

        result.Abstractions.Keys.Should().NotContain(k => k.StartsWith("TestProject.Tests."));
        result.Implementations.Keys.Should().NotContain(k => k.StartsWith("TestProject.Tests."));
    }

    #endregion

    #region NuGet dependency — AutoMapper.IMapper

    [Test]
    public async Task BuildMapAsync_AutoMapperAnimalMapper_AppearsInImplementations()
    {
        var result = await Act();

        result.Implementations.Should().ContainKey("TestProject.App.Mapping.AutoMapperAnimalMapper");
    }

    [Test]
    public async Task BuildMapAsync_AutoMapperAnimalMapper_HasIMapperDependency()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Mapping.AutoMapperAnimalMapper"];
        // AutoMapper.IMapper.Map<T>() is declared on IMapperBase — Roslyn resolves ContainingType to IMapperBase
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName.StartsWith("AutoMapper."));
    }

    [Test]
    public async Task BuildMapAsync_AutoMapperIMapper_HasNoSourceFile()
    {
        var result = await Act();

        // AutoMapper types come from NuGet, so SourceFilePath must be null
        var autoMapperAbstractions = result.Abstractions.Values
            .Where(a => a.FullName.StartsWith("AutoMapper."))
            .ToList();
        autoMapperAbstractions.Should().NotBeEmpty();
        autoMapperAbstractions.Should().AllSatisfy(a => a.SourceFilePath.Should().BeNull());
    }

    #endregion
}
