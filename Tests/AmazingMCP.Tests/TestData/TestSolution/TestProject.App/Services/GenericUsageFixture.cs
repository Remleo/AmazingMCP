using System.Collections.Generic;
using TestProject.Core.EventHandling;
using TestProject.Core.Models;

namespace TestProject.App.Services;

/// <summary>
/// Fixture for testing TypeAsGenericArgument and TypeAsGenericConstraint usage kinds.
/// </summary>

// TypeAsGenericArgument: Animal used as generic argument in base type list
public class AnimalCreatedHandler : IEventHandler<Animal, bool>
{
    public bool Handle(Animal evt)
    {
        return evt.Kind != AnimalKind.Unknown;
    }
}

// TypeAsGenericConstraint: Animal used in where constraint
public class GenericUsageFixture
{
    public T ProcessAnimal<T>(T input) where T : Animal
    {
        return input;
    }

    // TypeAsGenericArgument: Animal used as type argument in method call
    public IReadOnlyList<Animal> FilterAnimals(IEnumerable<Animal> source)
    {
        return new List<Animal>(source);
    }
}
