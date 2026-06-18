using TestProject.Core.Models;

namespace TestProject.Core.Persistence;

/// <summary>
/// Repository for animal entities. Supports lookup by id, kind, and count queries.
/// </summary>
public interface IAnimalRepository
{
    Animal? FindById(int id);
    IReadOnlyList<Animal> FindByKind(AnimalKind kind);
    void Save(Animal animal);
    int Count { get; }
    event EventHandler RepositoryChanged;
}
