namespace TestProject.Core.EventHandling;

/// <summary>
/// Dispatches domain events to registered handlers.
/// </summary>
public interface IEventDispatcher
{
    Task DispatchAsync<TEvent>(TEvent evt, CancellationToken ct = default);
}
