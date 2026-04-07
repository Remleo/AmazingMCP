using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.Infrastructure.Persistence;

public class SqlAnimalRepository : IAnimalRepository
{
    readonly List<Animal> _store = [];

    public Animal? FindById(int id) => _store.FirstOrDefault(a => a.Id == id);
    public IReadOnlyList<Animal> FindByKind(AnimalKind kind) => _store.Where(a => a.Kind == kind).ToList();
    public void Save(Animal animal) => _store.Add(animal);
    public int Count => _store.Count;
}
