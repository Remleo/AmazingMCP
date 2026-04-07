namespace TestProject.App.Messaging;

public interface IMessageHandler
{
    Task HandleAsync(object message, CancellationToken ct = default);
}

public interface IMessageHandler<TMessage> : IMessageHandler
{
    Task HandleAsync(TMessage message, CancellationToken ct = default);

    Task IMessageHandler.HandleAsync(object message, CancellationToken ct) => HandleAsync((TMessage) message, ct);
}