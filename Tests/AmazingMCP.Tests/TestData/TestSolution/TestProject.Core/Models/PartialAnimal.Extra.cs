namespace TestProject.Core.Models;

public partial class PartialAnimal
{
    public AnimalKind Kind { get; set; }
    public string GetDisplayName() => $"{Name} ({Kind})";
}
