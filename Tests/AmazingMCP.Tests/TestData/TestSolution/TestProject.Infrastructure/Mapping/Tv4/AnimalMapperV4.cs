using TestProject.Core.Dtos;
using TestProject.App.Mapping.Tv4;
using TestProject.Core.Models;

namespace TestProject.Infrastructure.Mapping.Tv4;

public class AnimalMapperV4 : IEntityMapperV4<Animal, AnimalDtoV4>
{
    public AnimalDtoV4 Map(Animal source) => new()
    {
        Id = source.Id,
        DisplayName = source.Name,
        Kind = source.Kind.ToString(),
        Description = $"{source.Kind} named {source.Name}",
        CreatedAt = DateTime.UtcNow,
        Tags = [source.Kind.ToString()]
    };

    public Animal MapBack(AnimalDtoV4 destination) => new()
    {
        Id = destination.Id,
        Name = destination.DisplayName
    };

    public bool CanMap(Animal source) => source.Id > 0;

    public AnimalDtoV4 MapPartial(Animal source, IReadOnlyList<string> fields) => Map(source);

    public Task<AnimalDtoV4> MapAsync(Animal source, CancellationToken ct = default)
        => Task.FromResult(Map(source));
}
