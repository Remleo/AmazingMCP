using TestProject.Core.Models;

namespace TestProject.Core.Persistence;

public interface IAnimalRepository
{
    Animal? FindById(int id);
    IReadOnlyList<Animal> FindByKind(AnimalKind kind);
    void Save(Animal animal);
    int Count { get; }
}
