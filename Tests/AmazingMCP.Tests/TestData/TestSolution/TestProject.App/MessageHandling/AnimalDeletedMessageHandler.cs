using TestProject.App.Contracts;
using TestProject.Core.EventHandling;
using TestProject.Core.Events;
using TestProject.App.Mapping;
using TestProject.App.Messaging;

namespace TestProject.App.MessageHandling;

public class AnimalDeletedMessageHandler : IMessageHandler<AnimalDeletedExternalDto>
{
    readonly IEntityMapper<AnimalDeletedExternalDto, AnimalDeletedEvent> _mapper;
    readonly IEventDispatcher _dispatcher;

    public AnimalDeletedMessageHandler(
        IEntityMapper<AnimalDeletedExternalDto, AnimalDeletedEvent> mapper,
        IEventDispatcher dispatcher)
    {
        _mapper = mapper;
        _dispatcher = dispatcher;
    }

    public async Task HandleAsync(AnimalDeletedExternalDto message, CancellationToken ct = default)
    {
        var evt = _mapper.Map(message);
        await _dispatcher.DispatchAsync(evt, ct);
    }
}
