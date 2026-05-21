using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.App.Persistence;

public class AnimalRepositoryV2 : RepositoryBase<Animal>
{
    readonly List<Animal> _store = [];

    public override Animal? GetById(int id) => _store.FirstOrDefault(a => a.Id == id);
    public override void Save(Animal entity) => _store.Add(entity);
}
