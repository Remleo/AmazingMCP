using AmazingMCP.Models;
using AmazingMCP.Models.Design;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Generic interface — abstraction

    [Test]
    public async Task BuildMapAsync_ClosedGenericInterface_AppearsInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey(
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    [Test]
    public async Task BuildMapAsync_ClosedGenericInterface_ListsImplementation()
    {
        var result = await Act();

        var abstraction = result.Abstractions[
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>"];
        abstraction.Implementations.Should().Contain("TestProject.App.Persistence.AnimalRepository");
    }

    #endregion

    #region Generic interface — implementation

    [Test]
    public async Task BuildMapAsync_ClosedGenericImplementation_AppearsInImplementations()
    {
        var result = await Act();

        result.Implementations.Should().ContainKey("TestProject.App.Persistence.AnimalRepository");
    }

    [Test]
    public async Task BuildMapAsync_ClosedGenericImplementation_ListsGenericAbstraction()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Persistence.AnimalRepository"];
        impl.ImplementedAbstractions.Should().Contain(
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    #endregion

    #region Generic interface with two type parameters

    [Test]
    public async Task BuildMapAsync_TwoTypeParamGenericInterface_AppearsInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey(
            "TestProject.Core.EventHandling.IEventHandler<TestProject.Core.Models.Animal, bool>");
    }

    [Test]
    public async Task BuildMapAsync_TwoTypeParamGenericImplementation_ListsAbstraction()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.Core.EventHandling.Handlers.AnimalEventHandler"];
        impl.ImplementedAbstractions.Should().Contain(
            "TestProject.Core.EventHandling.IEventHandler<TestProject.Core.Models.Animal, bool>");
    }

    #endregion

    #region Constructor dependency on closed generic — detected via method call

    [Test]
    public async Task BuildMapAsync_ClosedGenericDependency_DetectedViaMethodCall()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    [Test]
    public async Task BuildMapAsync_IEnumerableOfClosedGeneric_ElementTypeDetectedViaMethodCall()
    {
        var result = await Act();

        // GenericConsumerService iterates _handlers and calls handler.Handle(animal)
        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.Core.EventHandling.IEventHandler<TestProject.Core.Models.Animal, bool>");
    }

    #endregion

    #region Member usages on closed generic dependency

    [Test]
    public async Task BuildMapAsync_GenericDependency_MethodCallUsageDetected()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        var allUsages = impl.Dependencies.SelectMany(d => d.Usages).ToList();
        allUsages.Should().Contain(u => u.MemberName == "Save" && u.Kind == MemberUsageKind.MethodCall);
    }

    [Test]
    public async Task BuildMapAsync_GenericDependency_PropertyGetUsageDetected()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        var allUsages = impl.Dependencies.SelectMany(d => d.Usages).ToList();
        allUsages.Should().Contain(u => u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    [Test]
    public async Task BuildMapAsync_GenericDependency_MethodOnGenericHandlerDetected()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        var allUsages = impl.Dependencies.SelectMany(d => d.Usages).ToList();
        allUsages.Should().Contain(u => u.MemberName == "Handle" && u.Kind == MemberUsageKind.MethodCall);
    }

    #endregion

    #region Open generic interface

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_OpenGenericInterfaceAppearsInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey("TestProject.Core.Persistence.IRepository<T>");
    }

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_ListedAsImplementorOfOpenGeneric()
    {
        var result = await Act();

        result.Abstractions["TestProject.Core.Persistence.IRepository<T>"]
            .Implementations.Should().Contain("TestProject.App.Persistence.GenericRepository<T>");
    }

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_AppearsInImplementations()
    {
        var result = await Act();

        result.Implementations.Should().ContainKey("TestProject.App.Persistence.GenericRepository<T>");
    }

    #endregion
}
