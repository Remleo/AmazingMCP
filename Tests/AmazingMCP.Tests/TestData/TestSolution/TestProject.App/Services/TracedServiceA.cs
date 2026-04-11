using TestProject.Core.Logging;
using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

/// <summary>
/// Uses IGenericTracer&lt;TracedServiceA&gt; — a closed generic NuGet-style dependency.
/// Together with TracedServiceB, tests that both closed variants collapse into
/// IGenericTracer&lt;TService&gt; when the open generic is queried.
/// </summary>
public class TracedServiceA(
    IAnimalRepository repository,
    IGenericTracer<TracedServiceA>? tracer = null) : IAnimalService, ITracedService
{
    public Animal? GetById(int id)
    {
        using var _ = tracer?.Trace("GetById");
        return repository.FindById(id);
    }

    public IReadOnlyList<Animal> GetByKind(AnimalKind kind) =>
        repository.FindByKind(kind);

    public void Add(Animal animal) =>
        repository.Save(animal);

    public void Execute() { }
}
