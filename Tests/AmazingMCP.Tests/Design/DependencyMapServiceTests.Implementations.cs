using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.Design;

public partial class DependencyMapServiceTests
{
    #region Implementations — basic

    [Test]
    public async Task BuildMapAsync_ConcreteClass_AppearsInImplementations()
    {
        var result = await Act();

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
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.Services.IAnimalService");
    }

    #endregion

    #region Implementations — base class chain

    [Test]
    public async Task BuildMapAsync_ClassWithBaseClass_BaseClassChainPopulated()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.AdvancedAnimalService"];
        impl.BaseClasses.Should().Contain("TestProject.App.Services.AnimalServiceBase");
    }

    [Test]
    public async Task BuildMapAsync_ClassWithBaseClass_InterfaceFromBaseClassIncluded()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.AdvancedAnimalService"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.Services.IAnimalService");
    }

    [Test]
    public async Task BuildMapAsync_AbstractBaseClass_AppearsInImplementations()
    {
        var result = await Act();

        // AnimalServiceBase has a body with dependencies, so it gets its own Implementation entry
        result.Implementations.Should().ContainKey("TestProject.App.Services.AnimalServiceBase");
    }

    #endregion

    #region Implementations — multi-interface

    [Test]
    public async Task BuildMapAsync_MultiInterfaceClass_AllAbstractionsListed()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.MultiInterfaceService"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.Services.IAnimalService");
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.Services.INotificationService");
    }

    [Test]
    public async Task BuildMapAsync_MultiInterfaceClass_BothAbstractionsListThisImplementation()
    {
        var result = await Act();

        result.Abstractions["TestProject.Core.Services.IAnimalService"]
            .Implementations.Should().Contain("TestProject.App.Services.MultiInterfaceService");
        result.Abstractions["TestProject.Core.Services.INotificationService"]
            .Implementations.Should().Contain("TestProject.App.Services.MultiInterfaceService");
    }

    #endregion
}
