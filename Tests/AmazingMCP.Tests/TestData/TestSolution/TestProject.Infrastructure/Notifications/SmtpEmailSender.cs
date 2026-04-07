using TestProject.Core.Notifications;

namespace TestProject.Infrastructure.Notifications;

/// <summary>
/// SMTP-based email sender implementation.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        => Task.CompletedTask;
}
