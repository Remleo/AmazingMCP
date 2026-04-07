using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region IEnumerable<IMessageHandler> — non-generic interface injection

    [Test]
    public async Task BuildMapAsync_NonGenericIMessageHandler_AppearsInAbstractions()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey("TestProject.App.Messaging.IMessageHandler");
    }

    [Test]
    public async Task BuildMapAsync_NonGenericIMessageHandler_ListsConcreteHandlers()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions["TestProject.App.Messaging.IMessageHandler"];
        abstraction.Implementations.Should().Contain("TestProject.App.MessageHandling.AnimalCreatedMessageHandler");
        abstraction.Implementations.Should().Contain("TestProject.App.MessageHandling.AnimalDeletedMessageHandler");
    }

    [Test]
    public async Task BuildMapAsync_MessageDispatcher_IEnumerableOfIMessageHandlerUnwrapped()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Messaging.MessageDispatcher"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.App.Messaging.IMessageHandler" && d.IsEnumerable);
    }

    [Test]
    public async Task BuildMapAsync_MessageDispatcher_ImplementsIMessageConsumer()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Messaging.MessageDispatcher"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.App.Messaging.IMessageConsumer");
    }

    #endregion

    #region IEnumerable<IAsyncEventHandler> — non-generic interface injection

    [Test]
    public async Task BuildMapAsync_NonGenericIAsyncEventHandler_AppearsInAbstractions()
    {
        // act
        var result = await Act();

        // assert
        result.Abstractions.Should().ContainKey("TestProject.Core.EventHandling.IAsyncEventHandler");
    }

    [Test]
    public async Task BuildMapAsync_NonGenericIAsyncEventHandler_ListsConcreteHandlers()
    {
        // act
        var result = await Act();

        // assert
        var abstraction = result.Abstractions["TestProject.Core.EventHandling.IAsyncEventHandler"];
        abstraction.Implementations.Should().Contain("TestProject.Core.EventHandling.Handlers.AnimalDeletedEventHandler");
    }

    [Test]
    public async Task BuildMapAsync_EventDispatcher_IEnumerableOfIAsyncEventHandlerUnwrapped()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.Core.EventHandling.EventDispatcher"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.Core.EventHandling.IAsyncEventHandler" && d.IsEnumerable);
    }

    [Test]
    public async Task BuildMapAsync_EventDispatcher_ImplementsIEventDispatcher()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.Core.EventHandling.EventDispatcher"];
        impl.ImplementedAbstractions.Should().Contain("TestProject.Core.EventHandling.IEventDispatcher");
    }

    #endregion
}
