using TestProject.Core.Models;

// Top-level statements — used as a regression fixture for query_usages.
// query_usages must find the constructor call and property access below.
var animal = new Animal
{
    Id = 1,
    Name = "Rex",
    Kind = AnimalKind.Dog,
};

Console.WriteLine($"{animal.Name} ({animal.Kind})");
