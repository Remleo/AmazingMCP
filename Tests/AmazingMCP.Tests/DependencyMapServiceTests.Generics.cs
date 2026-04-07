using AmazingMCP.Models;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Generic interface — abstraction

    [Test]
    public async Task BuildMapAsync_ClosedGenericInterface_AppearsInAbstractions()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey(
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    [Test]
    public async Task BuildMapAsync_ClosedGenericInterface_DeclaredMembersCollected()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions[
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>"];
        abstraction.DeclaredMembers.Should().Contain("GetById()");
        abstraction.DeclaredMembers.Should().Contain("Save()");
        abstraction.DeclaredMembers.Should().Contain(m => m.Contains("Count"));
    }

    [Test]
    public async Task BuildMapAsync_ClosedGenericInterface_ListsImplementation()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions[
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>"];
        abstraction.Implementations.Should().Contain("TestProject.App.Persistence.AnimalRepository");
    }

    #endregion

    #region Generic interface — implementation

    [Test]
    public async Task BuildMapAsync_ClosedGenericImplementation_AppearsInImplementations()
    {
        // act
        var result = await Act();

        // assert
        result.Implementations.Should().ContainKey("TestProject.App.Persistence.AnimalRepository");
        result.Implementations["TestProject.App.Persistence.AnimalRepository"].Should().BeEquivalentTo(new
        {
            FullName = "TestProject.App.Persistence.AnimalRepository",
            Namespace = "TestProject.App.Persistence",
            ProjectName = "TestProject.App"
        }, options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task BuildMapAsync_ClosedGenericImplementation_ListsGenericAbstraction()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Persistence.AnimalRepository"];
        impl.ImplementedAbstractions.Should().Contain(
            "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    #endregion

    #region Generic interface with two type parameters

    [Test]
    public async Task BuildMapAsync_TwoTypeParamGenericInterface_AppearsInAbstractions()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey(
            "TestProject.Core.EventHandling.IEventHandler<TestProject.Core.Models.Animal, bool>");
    }

    [Test]
    public async Task BuildMapAsync_TwoTypeParamGenericImplementation_ListsAbstraction()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.Core.EventHandling.Handlers.AnimalEventHandler"];
        impl.ImplementedAbstractions.Should().Contain(
            "TestProject.Core.EventHandling.IEventHandler<TestProject.Core.Models.Animal, bool>");
    }

    #endregion

    #region Constructor dependency on closed generic

    [Test]
    public async Task BuildMapAsync_ConstructorDeps_ClosedGenericInterfaceDependencyDetected()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>" &&
            !d.IsOptions && !d.IsEnumerable);
    }

    [Test]
    public async Task BuildMapAsync_ConstructorDeps_IEnumerableOfClosedGenericUnwrapped()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.Core.EventHandling.IEventHandler<TestProject.Core.Models.Animal, bool>" &&
            d.IsEnumerable);
    }

    #endregion

    #region Member usages on closed generic dependency

    [Test]
    public async Task BuildMapAsync_GenericDependency_MethodCallUsageDetected()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Save" && u.Kind == MemberUsageKind.MethodCall);
    }

    [Test]
    public async Task BuildMapAsync_GenericDependency_PropertyGetUsageDetected()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    [Test]
    public async Task BuildMapAsync_GenericDependency_MethodOnGenericHandlerDetected()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.GenericConsumers.GenericConsumerService"];
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Handle" && u.Kind == MemberUsageKind.MethodCall);
    }

    #endregion

    #region Open generic interface

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_OpenGenericInterfaceAppearsInAbstractions()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey("TestProject.Core.Persistence.IRepository<T>");
    }

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_ListedAsImplementorOfOpenGeneric()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions["TestProject.Core.Persistence.IRepository<T>"]
            .Implementations.Should().Contain("TestProject.App.Persistence.GenericRepository<T>");
    }

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_AppearsInImplementations()
    {
        // act
        var result = await Act();

        // assert
        result.Implementations.Should().ContainKey("TestProject.App.Persistence.GenericRepository<T>");
    }

    [Test]
    public async Task BuildMapAsync_OpenGenericImplementation_ListsOpenGenericAbstraction()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Persistence.GenericRepository<T>"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.Persistence.IRepository<T>");
    }

    #endregion
}
