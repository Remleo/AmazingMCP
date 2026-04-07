using TestProject.Core.Models;

namespace TestProject.Core.Services;

public interface IAnimalService
{
    Animal? GetById(int id);
    IReadOnlyList<Animal> GetByKind(AnimalKind kind);
    void Add(Animal animal);
}
