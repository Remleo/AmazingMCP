namespace TestProject.Core.Notifications;

/// <summary>
/// Email sending abstraction.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
