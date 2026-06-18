using TestProject.Core.Models;
using TestProject.Infrastructure.Persistence;

namespace TestProject.Tests;

/// <summary>
/// Fixture: calls members via a concrete type (SqlAnimalRepository) that implicitly implements IAnimalRepository.
/// Used to test that usages via the concrete type are also reported for the interface.
/// </summary>
public class ConcreteTypeCallFixture(SqlAnimalRepository repo)
{
    // MethodCall via concrete type — should also appear as IAnimalRepository.FindById
    public Animal? GetById(int id) => repo.FindById(id);

    // PropertyRead via concrete type — should also appear as IAnimalRepository.Count
    public bool IsFull() => repo.Count >= 100;

    // PropertyWrite via concrete type — should also appear as IAnimalRepository.DefaultKind
    public void SetDefaultKind(AnimalKind kind) { repo.DefaultKind = kind; }

    // EventSubscribe via concrete type — should also appear as IAnimalRepository.RepositoryChanged
    public void Subscribe() => repo.RepositoryChanged += OnChanged;

    void OnChanged(object? sender, System.EventArgs e) { }
}
