using System.Collections.Generic;
using System.Linq;
using TestProject.Core.Models;
using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Helpers;

/// <summary>
/// Fixture class used exclusively by CodeLensServiceTests.
/// Contains a variety of constructs: local variables, method calls,
/// extension method calls, constructors, and type/method definitions.
/// </summary>
public class CodeLensTestFixture : IAnimalService
{
    readonly IAnimalRepository _repository;
    readonly INotificationService _notification;
    public AnimalKind DefaultKind { get; } = AnimalKind.Unknown;

    public CodeLensTestFixture(IAnimalRepository repository, INotificationService notification)
    {
        _repository = repository;
        _notification = notification;
    }

    // Method with local variable, method call, and extension call
    public Animal? GetById(int id)
    {
        var animal = _repository.FindById(id);
        return animal;
    }

    // Method with local variable of list type and LINQ extension
    public IReadOnlyList<Animal> GetByKind(AnimalKind kind)
    {
        var all = _repository.FindByKind(kind);
        var filtered = all.Where(a => a.Kind == kind).ToList();
        return filtered;
    }

    // Method with constructor call and multiple method calls
    public void Add(Animal animal)
    {
        var existing = _repository.FindById(animal.Id);
        if (existing != null)
        {
            _notification.Notify("Already exists");
            return;
        }

        _repository.Save(animal);
        _notification.Notify($"Saved {animal.Name}");
    }

    // Method with nullable local variable
    public Animal? FindOrDefault(int id)
    {
        Animal? result = _repository.FindById(id);
        return result;
    }

    // Method that reads a property — used by CodeLensServiceTests for Properties section
    public IReadOnlyList<Animal> GetByDefaultKind()
    {
        return _repository.FindByKind(DefaultKind);
    }
}
