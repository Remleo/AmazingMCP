using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.App.Helpers;

/// <summary>
/// Standalone class (no interface) with dependencies — used to test that
/// standalone classes pre-registered via EnsureAbstraction still get their
/// Implementations entry populated correctly.
/// </summary>
public class AnimalFormatter
{
    readonly IAnimalRepository _repository;

    public AnimalFormatter(IAnimalRepository repository)
    {
        _repository = repository;
    }

    public string FormatAnimal(int id)
    {
        var animal = _repository.FindById(id);
        return animal is null ? "unknown" : $"{animal.Name} ({animal.Kind})";
    }
}
