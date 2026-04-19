using TestProject.Core.Models;

namespace TestProject.Core.Services;

/// <summary>
/// Provides animal management operations including retrieval and persistence.
/// </summary>
public interface IAnimalService
{
    Animal? GetById(int id);
    IReadOnlyList<Animal> GetByKind(AnimalKind kind);
    void Add(Animal animal);
}
