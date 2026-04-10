using TestProject.Core.Services;

namespace TestProject.App.Helpers;

/// <summary>
/// Consumes AnimalFormatter (standalone class) — triggers EnsureAbstraction
/// for AnimalFormatter before it is scanned itself, which was the root cause
/// of the missing Implementations bug.
/// </summary>
public class AnimalFormatterConsumer : IAnimalService
{
    readonly AnimalFormatter _formatter;

    public AnimalFormatterConsumer(AnimalFormatter formatter)
    {
        _formatter = formatter;
    }

    public Core.Models.Animal? GetById(int id) => null;
    public IReadOnlyList<Core.Models.Animal> GetByKind(Core.Models.AnimalKind kind) => [];
    public void Add(Core.Models.Animal animal)
    {
        var label = _formatter.FormatAnimal(animal.Id);
        _ = label;
    }
}
