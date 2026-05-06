using System.Collections.Generic;
using TestProject.App.Helpers;
using TestProject.Core.Logging;
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
    readonly IGenericTracer<UsageQueryTestFixture> _tracer;

    // Field initializer — FieldWrite + ConstructorCall
    Animal _defaultAnimal = new Animal { Name = "Default", Kind = AnimalKind.Unknown };

    public UsageQueryTestFixture(
        IAnimalRepository repository,
        IGenericTracer<UsageQueryTestFixture> tracer)
    {
        _repository = repository;
        _tracer = tracer;
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

    // ── MethodCall on closed generic type ────────────────────────────────────

    public IDisposable TraceOperation(string operation)
    {
        // MethodCall on IGenericTracer<UsageQueryTestFixture> — receiver type is the closed generic
        return _tracer.Trace(operation);
    }

    // ── Extension-method-style call on closed generic type ───────────────────

    public void TraceAndFind(int id)
    {
        // MethodCall via extension method on IGenericTracer<UsageQueryTestFixture>
        using var scope = _tracer.Trace("TraceAndFind");
        _repository.FindById(id);
    }

    // ── Object initializer — property assigned from closed generic field ──────

    public TracerHolder BuildHolder()
    {
        // PropertyWrite via object initializer: Tracer = _tracer
        // TypeName of _tracer is IGenericTracer<UsageQueryTestFixture>
        return new TracerHolder
        {
            Tracer = _tracer,
        };
    }

    // ── Object initializer inside large lambda block ──────────────────────────

    public void UsageInObjectInitializerInsideLargeLambda(Animal animal)
    {
        // The outer lambda is >5 lines, but the usage is inside a compact
        // ObjectCreationExpression — section should span the new TracerHolder { ... } block
        // (multiple lines), not fall back to a single line at the identifier.
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            var line1 = animal.Name;
            var line2 = animal.Kind;
            var line3 = animal.Id;
            var line4 = "padding";
            var holder = new TracerHolder
            {
                Tracer = _tracer,
                ExtraField = "value",
            };
        });
    }

    // ── Private method — usages inside must still be found ───────────────────

    bool IsValidAnimal(Animal animal) => animal.Name.Length > 0;

    public bool CheckDefault() => IsValidAnimal(_defaultAnimal);       // implicit-this method call
    public bool CheckDefaultExplicit() => this.IsValidAnimal(_defaultAnimal); // explicit this. method call

    // ── Extension method call ─────────────────────────────────────────────────

    public string FormatAnimalLabel(Animal animal)
    {
        // MethodCall on AnimalExtensions — extension method call
        return animal.FormatLabel("Animal");
    }

    // ── Usage as parameter AND in body — definition must not be shown twice ──────

    public void ProcessAnimals(IReadOnlyList<Animal> animals)
    {
        // TypeAsParameter (animals) + MethodCall (FindById) — both reference Animal
        foreach (var a in animals)
            _repository.Save(a);
    }

    // ── MethodCall inside catch block inside large lambda ─────────────────────

    public void UsageInCatchInsideLargeLambda(Animal animal)
    {
        // The outer lambda is >5 lines. The usage is inside a catch block (small).
        // Section should be the catch block, not just the single invocation line.
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            var line1 = animal.Name;
            var line2 = animal.Kind;
            var line3 = animal.Id;
            var line4 = "padding";
            try
            {
                _repository.Save(animal);
            }
            catch (Exception)
            {
                _repository.FindById(animal.Id);
            }
        });
    }
}

/// <summary>Helper type for object-initializer usage test.</summary>
public class TracerHolder
{
    public IGenericTracer<UsageQueryTestFixture>? Tracer { get; set; }
    public string? ExtraField { get; set; }
}

/// <summary>Helper type for object-initializer with literal value test.</summary>
public class AnimalSnapshot
{
    public string? Name { get; set; }
    public AnimalKind Kind { get; set; }
}

public class UsageQueryObjectInitFixture
{
    // PropertyWrite via object initializer where value is a literal (not an identifier)
    public AnimalSnapshot BuildSnapshot() => new AnimalSnapshot
    {
        Name = "literal",
        Kind = AnimalKind.Unknown,
    };
}
