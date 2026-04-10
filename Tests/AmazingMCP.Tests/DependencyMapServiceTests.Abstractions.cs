using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Abstractions — interface with implementation

    [Test]
    public async Task BuildMapAsync_InterfaceWithImplementation_AppearsInAbstractions()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey("TestProject.Core.Services.IAnimalService");
        result.Abstractions["TestProject.Core.Services.IAnimalService"].Should().BeEquivalentTo(new
        {
            FullName = "TestProject.Core.Services.IAnimalService",
            Namespace = "TestProject.Core.Services",
            IsInterface = true
        }, options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task BuildMapAsync_InterfaceAbstraction_DeclaredMembersCollected()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions["TestProject.Core.Services.IAnimalService"];
        abstraction.DeclaredMembers.Should().BeEquivalentTo(new[]
        {
            "GetById()", "GetByKind()", "Add()"
        });
    }

    [Test]
    public async Task BuildMapAsync_InterfaceAbstraction_ListsAllImplementations()
    {
        // act
        var result = await Act();

        // assert
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
        // act
        var result = await Act();

        // assert
        result.Abstractions.Keys.Should().NotContain(k =>
            k.StartsWith("System.") || k.StartsWith("Microsoft.Extensions.Options."));
    }

    #endregion

    #region Standalone class — no interface, no complex deps → not in map

    [Test]
    public async Task BuildMapAsync_StandaloneClass_NotInAbstractions()
    {
        // act
        var result = await Act();

        // assert — StandaloneHelper has no interfaces and no constructor deps,
        // so it's not an abstraction or implementation under the new algorithm
        result.Abstractions.Should().NotContainKey("TestProject.App.Helpers.StandaloneHelper");
        result.Implementations.Should().NotContainKey("TestProject.App.Helpers.StandaloneHelper");
    }

    #endregion

    #region Abstractions — IOptions<T> unwrap

    [Test]
    public async Task BuildMapAsync_IOptionsType_AppearsAsAbstraction()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey("TestProject.Core.Configuration.AnimalSettings");
        result.Abstractions["TestProject.Core.Configuration.AnimalSettings"].Should().BeEquivalentTo(new
        {
            FullName = "TestProject.Core.Configuration.AnimalSettings",
            IsInterface = false
        }, options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task BuildMapAsync_IOptionsType_DeclaredMembersContainProperties()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions["TestProject.Core.Configuration.AnimalSettings"];
        abstraction.DeclaredMembers.Should().Contain(m => m.Contains("MaxAnimals"));
        abstraction.DeclaredMembers.Should().Contain(m => m.Contains("DefaultName"));
    }

    #endregion

    #region Abstractions — IAnimalRepository members

    [Test]
    public async Task BuildMapAsync_RepositoryAbstraction_DeclaredMembersIncludePropertyAndMethods()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions["TestProject.Core.Persistence.IAnimalRepository"];
        abstraction.DeclaredMembers.Should().Contain("FindById()");
        abstraction.DeclaredMembers.Should().Contain("FindByKind()");
        abstraction.DeclaredMembers.Should().Contain("Save()");
        abstraction.DeclaredMembers.Should().Contain(m => m.Contains("Count"));
    }

    #endregion

    #region Abstract classes excluded from implementations

    [Test]
    public async Task BuildMapAsync_AbstractClass_NotInImplementations()
    {
        // act
        var result = await Act();

        // assert — abstract classes are abstractions, not implementations
        result.Implementations.Should().NotContainKey("TestProject.App.Services.AnimalServiceBase");
    }

    #endregion

    #region Test project filtering

    [Test]
    public async Task BuildMapAsync_TestProjectTypes_ExcludedFromMap()
    {
        // act
        var result = await Act();

        // assert — TestProject.Tests is a test project (has Microsoft.NET.Test.Sdk),
        // so its types must not appear in the dependency map
        result.Abstractions.Keys.Should().NotContain(k => k.StartsWith("TestProject.Tests."));
        result.Implementations.Keys.Should().NotContain(k => k.StartsWith("TestProject.Tests."));
    }

    #endregion

    #region NuGet dependency — AutoMapper.IMapper

    [Test]
    public async Task BuildMapAsync_AutoMapperAnimalMapper_AppearsInImplementations()
    {
        // act
        var result = await Act();

        // assert
        result.Implementations.Should().ContainKey("TestProject.App.Mapping.AutoMapperAnimalMapper");
    }

    [Test]
    public async Task BuildMapAsync_AutoMapperAnimalMapper_HasIMapperDependency()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Mapping.AutoMapperAnimalMapper"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "AutoMapper.IMapper" && !d.IsOptions && !d.IsEnumerable);
    }

    [Test]
    public async Task BuildMapAsync_AutoMapperIMapper_HasNoSourceFile()
    {
        // act
        var result = await Act();

        // assert — IMapper comes from NuGet, so SourceFilePath must be null
        result.Abstractions.Should().ContainKey("AutoMapper.IMapper");
        result.Abstractions["AutoMapper.IMapper"].SourceFilePath.Should().BeNull();
    }

    #endregion
}
