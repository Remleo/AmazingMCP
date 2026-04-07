using TestProject.Core.Events;
using TestProject.App.Messaging;

namespace TestProject.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ-based message consumer implementation.
/// Depends on IMessageHandler to dispatch messages.
/// </summary>
public class RabbitMqConsumer : IMessageConsumer
{
    readonly IMessageHandler<AnimalCreatedEvent> _createdHandler;
    readonly IMessageHandler<AnimalDeletedEvent> _deletedHandler;

    public RabbitMqConsumer(
        IMessageHandler<AnimalCreatedEvent> createdHandler,
        IMessageHandler<AnimalDeletedEvent> deletedHandler)
    {
        _createdHandler = createdHandler;
        _deletedHandler = deletedHandler;
    }

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
}
