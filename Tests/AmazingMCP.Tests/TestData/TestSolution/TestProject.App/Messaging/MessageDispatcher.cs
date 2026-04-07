namespace TestProject.App.Messaging;

/// <summary>
/// Dispatches messages to all registered handlers via IEnumerable injection.
/// </summary>
public class MessageDispatcher : IMessageConsumer
{
    readonly IEnumerable<IMessageHandler> _handlers;

    public MessageDispatcher(IEnumerable<IMessageHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        foreach (var handler in _handlers)
            await handler.HandleAsync(new object(), ct);
    }

    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
}
