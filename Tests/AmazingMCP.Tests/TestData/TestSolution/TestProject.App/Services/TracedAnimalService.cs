using TestProject.Core.Logging;
using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

/// <summary>
/// Service that uses IOptionalTracer via:
/// 1. Direct null-conditional instance call: tracer?.StartTrace(...)
/// 2. Extension method via null-conditional: tracer?.TraceOperation(...)
/// Used to test that null-conditional invocations are correctly detected as dependencies.
/// </summary>
public class TracedAnimalService(
    IAnimalRepository repository,
    IOptionalTracer? tracer = null) : IAnimalService
{
    public Animal? GetById(int id)
    {
        // Instance method via null-conditional
        using var _ = tracer?.StartTrace("GetById");
        return repository.FindById(id);
    }

    public IReadOnlyList<Animal> GetByKind(AnimalKind kind)
    {
        // Extension method via null-conditional
        using var _ = tracer?.TraceOperation("GetByKind");
        return repository.FindByKind(kind);
    }

    public void Add(Animal animal) =>
        repository.Save(animal);
}
