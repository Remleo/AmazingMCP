using TestProject.Core.Dtos;
using TestProject.Core.Models;

namespace TestProject.App.Mapping;

public class AppAnimalMapper : IEntityMapper<Animal, AnimalDto>
{
    public AnimalDto Map(Animal source) => new()
    {
        Id = source.Id,
        DisplayName = source.Name,
        Kind = source.Kind.ToString()
    };

    public Animal MapBack(AnimalDto destination) => new()
    {
        Id = destination.Id,
        Name = destination.DisplayName
    };
}
