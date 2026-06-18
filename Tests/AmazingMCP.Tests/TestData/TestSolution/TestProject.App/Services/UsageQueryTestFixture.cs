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

    // ── ReturnType ──────────────────────────────────────────────────────

    public Animal GetOrCreate(int id)
    {
        // ReturnType: Animal (return type of this method)
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
    public void SaveDefault() => _repository.Save(_defaultAnimal);    // implicit-this field read as argument

    // ── Extension method call ─────────────────────────────────────────────────

    public string FormatAnimalLabel(Animal animal)
    {
        // MethodCall on AnimalExtensions — extension method call
        return animal.FormatLabel("Animal");
    }

    // ── Usage as parameter AND in body — definition must not be shown twice ──────

    public void ProcessAnimals(IReadOnlyList<Animal> animals)
    {
        // Parameter (animals) + MethodCall (FindById) — both reference Animal
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

    // ── typeof ────────────────────────────────────────────────────────────────

    public Type GetAnimalType()
    {
        // TypeOf: Animal
        return typeof(Animal);
    }

    public Type GetRepositoryType()
    {
        // TypeOf: IAnimalRepository
        return typeof(IAnimalRepository);
    }

    // ── nameof — type only ────────────────────────────────────────────────────

    public string GetAnimalTypeName()
    {
        // NameOf: Animal (type only)
        return nameof(Animal);
    }

    // ── nameof — member of current class ─────────────────────────────────────

    public string GetThisMethodName()
    {
        // NameOf: UsageQueryTestFixture, MethodName = GetThisMethodName
        return nameof(GetThisMethodName);
    }

    public string GetThisTypeName()
    {
        // NameOf: UsageQueryTestFixture (type, implicit this context)
        return nameof(UsageQueryTestFixture);
    }

    // ── nameof — member ───────────────────────────────────────────────────────

    public string GetAnimalNamePropertyName()
    {
        // NameOf: Animal, PropertyName = Name
        return nameof(Animal.Name);
    }

    public string GetInstanceMemberName(Animal animal)
    {
        // NameOf via instance: Animal, PropertyName = Name (instance access, not type access)
        return nameof(animal.Name);
    }

    public string GetKindPropertyName()
    {
        // NameOf: Animal, PropertyName = Kind
        return nameof(Animal.Kind);
    }

    public Type GetOpenGenericListType()
    {
        // TypeOf: open generic — System.Collections.Generic.List<T>
        return typeof(List<>);
    }

    // ── Event subscribe / unsubscribe ─────────────────────────────────────────

    public void SubscribeToRepository(IAnimalRepository repository)
    {
        // EventSubscribe: RepositoryChanged
        repository.RepositoryChanged += OnRepositoryChanged;
    }

    public void UnsubscribeFromRepository(IAnimalRepository repository)
    {
        // EventUnsubscribe: RepositoryChanged
        repository.RepositoryChanged -= OnRepositoryChanged;
    }

    void OnRepositoryChanged(object? sender, System.EventArgs e) { }
}

/// <summary>Fixture: class constant used inside an attribute on a method parameter.</summary>
public class ConstantInParamAttributeFixture
{
    public void Process(
        [System.ComponentModel.DefaultValue(AnimalDefaults.MaxNameLength)] IAnimalRepository repository) { }
}

/// <summary>Fixture: nameof used inside an attribute argument.</summary>
[System.ComponentModel.DisplayName(nameof(Animal))]
public class NameOfInAttributeFixture;

/// <summary>Fixture: nameof used inside an attribute on a primary constructor parameter.</summary>
public class NameOfInPrimaryCtorParamAttributeFixture(
    [System.ComponentModel.DefaultValue(nameof(Animal))] IAnimalRepository repository);

/// <summary>Fixture: nameof used inside an attribute on a method.</summary>
public class NameOfInMethodAttributeFixture
{
    [System.ComponentModel.DisplayName(nameof(Animal))]
    public void Process(IAnimalRepository repository) { }

    public void ProcessWithAttributedParam(
        [System.ComponentModel.DefaultValue(nameof(Animal))] IAnimalRepository repository) { }
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

/// <summary>Fixture: using static — calls static members of Animal-related types without qualifier.</summary>
public class UsingStaticFixture
{
    // MethodCall via using static: AnimalDefaults.BuildDefaultName called without qualifier
    public string GetDefaultName(int id) => AnimalDefaults.BuildDefaultName(id);

    // FieldRead via using static: AnimalDefaults.MaxNameLength read without qualifier
    public int GetMaxLength() => AnimalDefaults.MaxNameLength;

    // PropertyRead via using static: AnimalDefaults.MaxAllowed read without qualifier
    public int GetMaxAllowed() => AnimalDefaults.MaxAllowed;
}

/// <summary>Fixture: is/as pattern matching against Animal.</summary>
public class IsAsPatternFixture
{
    // AsIsPattern: obj is Animal
    public bool IsAnimal(object obj) => obj is Animal;

    // IsPattern: obj is Animal a (declaration pattern)
    public string? GetAnimalName(object obj)
    {
        if (obj is Animal a)
            return a.Name;
        return null;
    }

    // AsPattern: obj as Animal
    public Animal? AsAnimal(object obj) => obj as Animal;
}

/// <summary>Fixture: event ?.Invoke() — treated as EventCall on the declaring type.</summary>
public class EventCallFixture
{
    event EventHandler? StatusChanged;

    // EventCall: StatusChanged?.Invoke(...)
    public void NotifyStatusChanged()
    {
        StatusChanged?.Invoke(this, System.EventArgs.Empty);
    }

    // EventCall: direct invocation without ?.Invoke
    public void NotifyStatusChangedDirect()
    {
        StatusChanged(this, System.EventArgs.Empty);
    }
}
