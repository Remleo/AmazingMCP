using TestProject.Core.Dtos;
using TestProject.App.Mapping.Tv2;
using TestProject.Core.Models;

namespace TestProject.App.Mapping;

public class AppAnimalMapperV2 : IEntityMapperV2<Animal, AnimalDtoV2>
{
    public AnimalDtoV2 Map(Animal source) => new()
    {
        Id = source.Id,
        DisplayName = source.Name,
        Kind = source.Kind.ToString(),
        Description = $"{source.Kind}: {source.Name}"
    };

    public Animal MapBack(AnimalDtoV2 destination) => new()
    {
        Id = destination.Id,
        Name = destination.DisplayName
    };

    public bool CanMap(Animal source) => source.Id > 0;
}
