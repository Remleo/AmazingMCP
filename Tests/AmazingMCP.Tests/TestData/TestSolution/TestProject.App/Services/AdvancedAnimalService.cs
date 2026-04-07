using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

public class AdvancedAnimalService : AnimalServiceBase, IAnimalService
{
    readonly INotificationService _notification;

    public AdvancedAnimalService(
        IAnimalRepository repository,
        INotificationService notification)
        : base(repository)
    {
        _notification = notification;
    }

    public Animal? GetById(int id) =>
        Repository.FindById(id);

    public IReadOnlyList<Animal> GetByKind(AnimalKind kind) =>
        Repository.FindByKind(kind);

    public void Add(Animal animal)
    {
        var count = GetTotalCount();
        Repository.Save(animal);
        _notification.Notify($"Added animal #{count + 1}");
    }
}
