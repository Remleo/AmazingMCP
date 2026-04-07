using TestProject.App.Contracts;
using TestProject.Core.Events;

namespace TestProject.App.Mapping;

public class AnimalDeletedEventMapper : IEntityMapper<AnimalDeletedExternalDto, AnimalDeletedEvent>
{
    public AnimalDeletedEvent Map(AnimalDeletedExternalDto source) => new()
    {
        AnimalId = source.ExternalAnimalId
    };

    public AnimalDeletedExternalDto MapBack(AnimalDeletedEvent destination) => new()
    {
        ExternalAnimalId = destination.AnimalId
    };
}
