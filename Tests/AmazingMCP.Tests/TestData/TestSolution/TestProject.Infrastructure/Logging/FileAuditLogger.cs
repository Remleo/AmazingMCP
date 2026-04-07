using TestProject.Core.Logging;

namespace TestProject.Infrastructure.Logging;

public class FileAuditLogger : IAuditLogger
{
    public Task LogAsync(string action, string entityType, string entityId, CancellationToken ct = default)
        => Task.CompletedTask;
}
