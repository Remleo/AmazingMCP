namespace TestProject.App.Contracts;

/// <summary>
/// External DTO received from message broker for animal deletion.
/// </summary>
public class AnimalDeletedExternalDto
{
    public int ExternalAnimalId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
