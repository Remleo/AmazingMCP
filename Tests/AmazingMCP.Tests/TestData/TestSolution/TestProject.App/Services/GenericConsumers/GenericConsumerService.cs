using TestProject.Core.EventHandling;
using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.App.Services.GenericConsumers;

public class GenericConsumerService
{
    readonly IRepository<Animal> _repository;
    readonly IEnumerable<IEventHandler<Animal, bool>> _handlers;

    public GenericConsumerService(
        IRepository<Animal> repository,
        IEnumerable<IEventHandler<Animal, bool>> handlers)
    {
        _repository = repository;
        _handlers = handlers;
    }

    public void AddIfValid(Animal animal)
    {
        foreach (var handler in _handlers)
        {
            if (!handler.Handle(animal))
                return;
        }
        _repository.Save(animal);
    }

    public int GetCount() => _repository.Count;
}
