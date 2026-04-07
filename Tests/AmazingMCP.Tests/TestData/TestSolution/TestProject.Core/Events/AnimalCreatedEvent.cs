namespace TestProject.Core.Events;

public class AnimalCreatedEvent
{
    public int AnimalId { get; set; }
    public string Name { get; set; } = string.Empty;
}
