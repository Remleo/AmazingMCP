using TestProject.Core.Events;
using TestProject.Core.Logging;
using TestProject.Core.Notifications;
using TestProject.Core.Persistence;

namespace TestProject.Core.EventHandling.Handlers;

public class AnimalCreatedEventHandler : IAsyncEventHandler<AnimalCreatedEvent>
{
    readonly IAnimalRepository _repository;
    readonly IEmailSender _emailSender;
    readonly IAuditLogger _auditLogger;

    public AnimalCreatedEventHandler(
        IAnimalRepository repository,
        IEmailSender emailSender,
        IAuditLogger auditLogger)
    {
        _repository = repository;
        _emailSender = emailSender;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(AnimalCreatedEvent evt, CancellationToken ct = default)
    {
        var animal = _repository.FindById(evt.AnimalId);
        if (animal is null) return;

        await _emailSender.SendAsync(
            "[email]", "New Animal", $"Animal {animal.Name} created", ct);

        await _auditLogger.LogAsync("Created", "Animal", evt.AnimalId.ToString(), ct);
    }
}
