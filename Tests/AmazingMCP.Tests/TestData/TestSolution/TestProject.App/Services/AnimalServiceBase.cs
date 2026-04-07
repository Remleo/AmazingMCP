using TestProject.Core.Persistence;

namespace TestProject.App.Services;

public abstract class AnimalServiceBase
{
    protected readonly IAnimalRepository Repository;

    protected AnimalServiceBase(IAnimalRepository repository)
    {
        Repository = repository;
    }

    protected int GetTotalCount() => Repository.Count;
}
