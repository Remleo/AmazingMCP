using Microsoft.Extensions.Options;
using TestProject.Core.Configuration;
using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

public class AnimalService : IAnimalService
{
    readonly IAnimalRepository _repository;
    readonly INotificationService _notification;
    readonly AnimalSettings _settings;

    public AnimalService(
        IAnimalRepository repository,
        INotificationService notification,
        IOptions<AnimalSettings> settings)
    {
        _repository = repository;
        _notification = notification;
        _settings = settings.Value;
    }

    public Animal? GetById(int id) =>
        _repository.FindById(id);

    public IReadOnlyList<Animal> GetByKind(AnimalKind kind) =>
        _repository.FindByKind(kind);

    public void Add(Animal animal)
    {
        if (_repository.Count >= _settings.MaxAnimals)
        {
            _notification.Notify("Max animals reached");
            return;
        }

        _repository.Save(animal);
        _notification.Notify($"Added {animal.Name}");
    }
}
