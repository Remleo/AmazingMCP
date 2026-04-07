using TestProject.Core.Dtos;
using TestProject.App.Mapping.Tv3;
using TestProject.Core.Models;

namespace TestProject.Infrastructure.Mapping.Tv3;

public class AnimalMapperV3 : IEntityMapperV3<Animal, AnimalDtoV3>
{
    public AnimalDtoV3 Map(Animal source) => new()
    {
        Id = source.Id,
        DisplayName = source.Name,
        Kind = source.Kind.ToString(),
        Description = $"{source.Kind} named {source.Name}",
        CreatedAt = DateTime.UtcNow
    };

    public Animal MapBack(AnimalDtoV3 destination) => new()
    {
        Id = destination.Id,
        Name = destination.DisplayName
    };

    public bool CanMap(Animal source) => source.Id > 0;

    public AnimalDtoV3 MapPartial(Animal source, IReadOnlyList<string> fields) => Map(source);
}
