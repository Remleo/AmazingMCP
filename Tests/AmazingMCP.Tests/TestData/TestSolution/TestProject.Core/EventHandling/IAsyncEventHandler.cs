namespace TestProject.Core.EventHandling;

/// <summary>
/// Non-generic async event handler abstraction — allows IEnumerable injection.
/// </summary>
public interface IAsyncEventHandler
{
    Task HandleAsync(object evt, CancellationToken ct = default);
}

/// <summary>
/// Async event handler abstraction for domain events.
/// </summary>
public interface IAsyncEventHandler<TEvent> : IAsyncEventHandler
{
    Task HandleAsync(TEvent evt, CancellationToken ct = default);

    Task IAsyncEventHandler.HandleAsync(object evt, CancellationToken ct) => HandleAsync((TEvent) evt, ct);
}
