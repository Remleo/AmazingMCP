namespace TestProject.Core.Logging;

public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string entityId, CancellationToken ct = default);
}
