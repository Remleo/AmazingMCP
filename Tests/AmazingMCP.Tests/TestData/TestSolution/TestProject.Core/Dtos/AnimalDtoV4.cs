namespace TestProject.Core.Dtos;

public class AnimalDtoV4
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];
}
