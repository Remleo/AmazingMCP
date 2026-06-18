using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.Infrastructure.Persistence;

public class SqlAnimalRepository : IAnimalRepository
{
    readonly List<Animal> _store = [];

    public Animal? FindById(int id) => _store.FirstOrDefault(a => a.Id == id);
    public IReadOnlyList<Animal> FindByKind(AnimalKind kind) => _store.Where(a => a.Kind == kind).ToList();
    public void Save(Animal animal)
    {
        _store.Add(animal);
        // EventCall: RepositoryChanged?.Invoke(...) — should appear as IAnimalRepository.RepositoryChanged
        RepositoryChanged?.Invoke(this, EventArgs.Empty);
    }
    public int Count => _store.Count;
    public AnimalKind DefaultKind { get; set; }
    public event EventHandler RepositoryChanged = delegate { };

    // ── Implicit-this usages — member used inside the class without receiver ──

    // MethodCall implicit-this: FindById called without this. — should appear as IAnimalRepository.FindById
    public bool ContainsId(int id) => FindById(id) is not null;

    // PropertyRead implicit-this: Count read without this. — should appear as IAnimalRepository.Count
    public bool IsEmpty() => Count == 0;

    // PropertyWrite implicit-this: DefaultKind written without this. — should appear as IAnimalRepository.DefaultKind
    public void ResetDefaultKind() { DefaultKind = AnimalKind.Unknown; }
}
