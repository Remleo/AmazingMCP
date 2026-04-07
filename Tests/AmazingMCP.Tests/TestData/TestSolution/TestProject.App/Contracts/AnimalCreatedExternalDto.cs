namespace TestProject.App.Contracts;

/// <summary>
/// External DTO received from message broker for animal creation.
/// </summary>
public class AnimalCreatedExternalDto
{
    public int ExternalAnimalId { get; set; }
    public string ExternalName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
