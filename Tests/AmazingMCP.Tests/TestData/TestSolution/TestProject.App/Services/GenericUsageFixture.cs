using System.Collections.Generic;
using TestProject.Core.EventHandling;
using TestProject.Core.Models;

namespace TestProject.App.Services;

/// <summary>
/// Fixture for testing GenericArgument and GenericConstraint usage kinds.
/// </summary>

// GenericArgument: Animal used as generic argument in base type list
public class AnimalCreatedHandler : IEventHandler<Animal, bool>
{
    public bool Handle(Animal evt)
    {
        return evt.Kind != AnimalKind.Unknown;
    }
}

// GenericConstraint: Animal used in where constraint
public class GenericUsageFixture
{
    public T ProcessAnimal<T>(T input) where T : Animal
    {
        return input;
    }

    // GenericArgument: Animal used as type argument in method call
    public IReadOnlyList<Animal> FilterAnimals(IEnumerable<Animal> source)
    {
        return new List<Animal>(source);
    }
}
