using TestProject.Core.Models;

namespace TestProject.Core.EventHandling.Handlers;

public class AnimalEventHandler : IEventHandler<Animal, bool>
{
    public bool Handle(Animal evt) => evt.Id > 0;
}
