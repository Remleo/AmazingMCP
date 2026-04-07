namespace TestProject.Core.Models;

public class Animal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AnimalKind Kind { get; set; }
}
