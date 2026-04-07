using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Implementations — basic

    [Test]
    public async Task BuildMapAsync_ConcreteClass_AppearsInImplementations()
    {
        // act
        var result = await Act();

        // assert
        result.Implementations.Should().ContainKey("TestProject.App.Services.AnimalService");
        result.Implementations["TestProject.App.Services.AnimalService"].Should().BeEquivalentTo(new
        {
            FullName = "TestProject.App.Services.AnimalService",
            Namespace = "TestProject.App.Services",
            ProjectName = "TestProject.App"
        }, options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task BuildMapAsync_Implementation_ListsImplementedAbstractions()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.ImplementedAbstractions.Should().BeEquivalentTo(new[]
        {
            "TestProject.Core.Services.IAnimalService"
        });
    }

    #endregion

    #region Implementations — base class chain

    [Test]
    public async Task BuildMapAsync_ClassWithBaseClass_BaseClassChainPopulated()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AdvancedAnimalService"];
        impl.BaseClasses.Should().Contain("TestProject.App.Services.AnimalServiceBase");
    }

    [Test]
    public async Task BuildMapAsync_ClassWithBaseClass_InterfaceFromBaseClassIncluded()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AdvancedAnimalService"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.Services.IAnimalService");
    }

    #endregion

    #region Implementations — multi-interface

    [Test]
    public async Task BuildMapAsync_MultiInterfaceClass_AllAbstractionsListed()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.MultiInterfaceService"];
        impl.ImplementedAbstractions.Should().BeEquivalentTo(new[]
        {
            "TestProject.Core.Services.IAnimalService",
            "TestProject.Core.Services.INotificationService"
        });
    }

    [Test]
    public async Task BuildMapAsync_MultiInterfaceClass_BothAbstractionsListThisImplementation()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions["TestProject.Core.Services.IAnimalService"]
            .Implementations.Should().Contain("TestProject.App.Services.MultiInterfaceService");
        result.Abstractions["TestProject.Core.Services.INotificationService"]
            .Implementations.Should().Contain("TestProject.App.Services.MultiInterfaceService");
    }

    #endregion
}
