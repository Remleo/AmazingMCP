namespace TestProject.Core.EventHandling;

/// <summary>
/// Dispatches domain events to all registered handlers via IEnumerable injection.
/// </summary>
public class EventDispatcher : IEventDispatcher
{
    readonly IEnumerable<IAsyncEventHandler> _handlers;

    public EventDispatcher(IEnumerable<IAsyncEventHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task DispatchAsync<TEvent>(TEvent evt, CancellationToken ct = default)
    {
        foreach (var handler in _handlers)
            await handler.HandleAsync(evt!, ct);
    }
}
