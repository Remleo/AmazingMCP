using TestProject.Core.Events;
using TestProject.Core.Logging;
using TestProject.Core.Notifications;

namespace TestProject.Core.EventHandling.Handlers;

public class AnimalDeletedEventHandler : IAsyncEventHandler<AnimalDeletedEvent>
{
    readonly IEmailSender _emailSender;
    readonly IAuditLogger _auditLogger;

    public AnimalDeletedEventHandler(
        IEmailSender emailSender,
        IAuditLogger auditLogger)
    {
        _emailSender = emailSender;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(AnimalDeletedEvent evt, CancellationToken ct = default)
    {
        await _emailSender.SendAsync(
            "[email]", "Animal Deleted", $"Animal {evt.AnimalId} removed", ct);

        await _auditLogger.LogAsync("Deleted", "Animal", evt.AnimalId.ToString(), ct);
    }
}
