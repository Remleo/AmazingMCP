using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region IEnumerable<IMessageHandler> — element type detected via method call

    [Test]
    public async Task BuildMapAsync_NonGenericIMessageHandler_AppearsInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey("TestProject.App.Messaging.IMessageHandler");
    }

    [Test]
    public async Task BuildMapAsync_NonGenericIMessageHandler_ListsConcreteHandlers()
    {
        var result = await Act();

        var abstraction = result.Abstractions["TestProject.App.Messaging.IMessageHandler"];
        abstraction.Implementations.Should().Contain("TestProject.App.MessageHandling.AnimalCreatedMessageHandler");
        abstraction.Implementations.Should().Contain("TestProject.App.MessageHandling.AnimalDeletedMessageHandler");
    }

    [Test]
    public async Task BuildMapAsync_MessageDispatcher_IMessageHandlerDetectedViaMethodCall()
    {
        var result = await Act();

        // MessageDispatcher iterates _handlers and calls handler.HandleAsync(...)
        var impl = result.Implementations["TestProject.App.Messaging.MessageDispatcher"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.App.Messaging.IMessageHandler");
    }

    [Test]
    public async Task BuildMapAsync_MessageDispatcher_ImplementsIMessageConsumer()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Messaging.MessageDispatcher"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.App.Messaging.IMessageConsumer");
    }

    #endregion

    #region IEnumerable<IAsyncEventHandler> — element type detected via method call

    [Test]
    public async Task BuildMapAsync_NonGenericIAsyncEventHandler_AppearsInAbstractions()
    {
        var result = await Act();

        result.Abstractions.Should().ContainKey("TestProject.Core.EventHandling.IAsyncEventHandler");
    }

    [Test]
    public async Task BuildMapAsync_NonGenericIAsyncEventHandler_ListsConcreteHandlers()
    {
        var result = await Act();

        var abstraction = result.Abstractions["TestProject.Core.EventHandling.IAsyncEventHandler"];
        abstraction.Implementations.Should().Contain("TestProject.Core.EventHandling.Handlers.AnimalDeletedEventHandler");
    }

    [Test]
    public async Task BuildMapAsync_EventDispatcher_IAsyncEventHandlerDetectedViaMethodCall()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.Core.EventHandling.EventDispatcher"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.Core.EventHandling.IAsyncEventHandler");
    }

    [Test]
    public async Task BuildMapAsync_EventDispatcher_ImplementsIEventDispatcher()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.Core.EventHandling.EventDispatcher"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.EventHandling.IEventDispatcher");
    }

    #endregion
}
