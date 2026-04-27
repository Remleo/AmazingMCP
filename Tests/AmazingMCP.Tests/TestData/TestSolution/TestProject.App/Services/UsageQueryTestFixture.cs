using System.Collections.Generic;
using TestProject.Core.Models;
using TestProject.Core.Persistence;

namespace TestProject.App.Services;

/// <summary>
/// Fixture class that intentionally contains a variety of usage patterns
/// for testing the QueryUsages tool.
/// </summary>
public class UsageQueryTestFixture
{
    readonly IAnimalRepository _repository;

    // Field initializer — FieldWrite + ConstructorCall
    Animal _defaultAnimal = new Animal { Name = "Default", Kind = AnimalKind.Unknown };

    public UsageQueryTestFixture(IAnimalRepository repository)
    {
        _repository = repository;
    }

    // ── MethodCall ────────────────────────────────────────────────────────────

    public Animal? FindAnimalById(int id)
    {
        // MethodCall: FindById
        return _repository.FindById(id);
    }

    // ── PropertyRead ──────────────────────────────────────────────────────────

    public bool IsRepositoryFull()
    {
        // PropertyRead: Count
        return _repository.Count >= 100;
    }

    // ── PropertyWrite ─────────────────────────────────────────────────────────

    public void RenameAnimal(Animal animal, string newName)
    {
        // PropertyWrite: Name
        animal.Name = newName;
    }

    // ── FieldRead / FieldWrite ────────────────────────────────────────────────

    public Animal GetDefault()
    {
        // FieldRead: _defaultAnimal
        return _defaultAnimal;
    }

    public void ResetDefault()
    {
        // FieldWrite: _defaultAnimal
        _defaultAnimal = new Animal { Name = "Reset", Kind = AnimalKind.Unknown };
    }

    // ── ConstructorCall ───────────────────────────────────────────────────────

    public Animal CreateAnimal(string name, AnimalKind kind)
    {
        // ConstructorCall: Animal
        return new Animal { Name = name, Kind = kind };
    }

    // ── TypeAsReturnType ──────────────────────────────────────────────────────

    public Animal GetOrCreate(int id)
    {
        // TypeAsReturnType: Animal (return type of this method)
        return _repository.FindById(id) ?? new Animal { Name = "New", Kind = AnimalKind.Unknown };
    }

    // ── LocalVariableInit ─────────────────────────────────────────────────────

    public IReadOnlyList<Animal> GetByKindWithLog(AnimalKind kind)
    {
        // LocalVariableInit: result
        var result = _repository.FindByKind(kind);
        return result;
    }

    // ── ReturnStatement ───────────────────────────────────────────────────────

    public int GetCount()
    {
        // ReturnStatement containing PropertyRead: Count
        return _repository.Count;
    }

    // ── ThrowStatement ────────────────────────────────────────────────────────

    public Animal FindOrThrow(int id)
    {
        var animal = _repository.FindById(id);
        if (animal is null)
            throw new KeyNotFoundException($"Animal {id} not found");
        return animal;
    }

    // ── IfCondition ───────────────────────────────────────────────────────────

    public void SaveIfNotFull(Animal animal)
    {
        // IfCondition containing PropertyRead: Count
        if (_repository.Count < 100)
            _repository.Save(animal);
    }

    // ── Multiple non-adjacent usages in one method ────────────────────────────

    public void MultiUsageMethod(Animal animal)
    {
        // First usage — near top of method
        var found = _repository.FindById(animal.Id);

        // Gap — unrelated code
        var name = animal.Name;
        var kind = animal.Kind;
        var description = $"{name} ({kind})";

        // Second usage — further down, non-adjacent to first
        if (_repository.Count < 100)
            _repository.Save(animal);
    }

    // ── Usage inside large lambda block ──────────────────────────────────────

    public void UsageInsideLargeLambda(Animal animal)
    {
        // The lambda block is >5 lines — usage inside should NOT capture the whole lambda as section
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            var line1 = animal.Name;
            var line2 = animal.Kind;
            var line3 = animal.Id;
            _repository.Save(animal);
            var line5 = "done";
            var line6 = "extra";
        });
    }
}
