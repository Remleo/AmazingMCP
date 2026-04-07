using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

public class MultiInterfaceService : IAnimalService, INotificationService
{
    readonly IAnimalRepository _repository;

    public MultiInterfaceService(IAnimalRepository repository)
    {
        _repository = repository;
    }

    public Animal? GetById(int id) => _repository.FindById(id);
    public IReadOnlyList<Animal> GetByKind(AnimalKind kind) => _repository.FindByKind(kind);
    public void Add(Animal animal) => _repository.Save(animal);
    public void Notify(string message) { }
}
