using TestProject.Core.Logging;
using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

/// <summary>
/// Uses IGenericTracer&lt;TracedServiceB&gt; — second closed variant.
/// Together with TracedServiceA, tests that both collapse into IGenericTracer&lt;TService&gt;.
/// </summary>
public class TracedServiceB(
    IAnimalRepository repository,
    IGenericTracer<TracedServiceB>? tracer = null) : IAnimalService, ITracedService
{
    public Animal? GetById(int id) =>
        repository.FindById(id);

    public IReadOnlyList<Animal> GetByKind(AnimalKind kind)
    {
        using var _ = tracer?.Trace("GetByKind");
        return repository.FindByKind(kind);
    }

    public void Add(Animal animal) =>
        repository.Save(animal);

    public void Execute() { }
}
