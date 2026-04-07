namespace TestProject.Core.Dtos;

public class AnimalDtoV3
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
