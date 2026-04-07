namespace TestProject.App.Messaging;

public interface IMessageConsumer
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
