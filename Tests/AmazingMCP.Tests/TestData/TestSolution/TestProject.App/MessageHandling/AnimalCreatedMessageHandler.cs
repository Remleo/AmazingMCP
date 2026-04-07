using TestProject.App.Contracts;
using TestProject.Core.EventHandling;
using TestProject.Core.Events;
using TestProject.App.Mapping;
using TestProject.App.Messaging;

namespace TestProject.App.MessageHandling;

public class AnimalCreatedMessageHandler : IMessageHandler<AnimalCreatedExternalDto>
{
    readonly IEntityMapper<AnimalCreatedExternalDto, AnimalCreatedEvent> _mapper;
    readonly IEventDispatcher _dispatcher;

    public AnimalCreatedMessageHandler(
        IEntityMapper<AnimalCreatedExternalDto, AnimalCreatedEvent> mapper,
        IEventDispatcher dispatcher)
    {
        _mapper = mapper;
        _dispatcher = dispatcher;
    }

    public async Task HandleAsync(AnimalCreatedExternalDto message, CancellationToken ct = default)
    {
        var evt = _mapper.Map(message);
        await _dispatcher.DispatchAsync(evt, ct);
    }
}
