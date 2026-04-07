using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.App.Persistence;

public class AnimalRepository : IRepository<Animal>
{
    readonly List<Animal> _store = [];

    public Animal? GetById(int id) => _store.FirstOrDefault(a => a.Id == id);
    public void Save(Animal entity) => _store.Add(entity);
    public int Count => _store.Count;
}
