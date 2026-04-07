using TestProject.App.Contracts;
using TestProject.Core.Events;

namespace TestProject.App.Mapping;

public class AnimalCreatedEventMapper : IEntityMapper<AnimalCreatedExternalDto, AnimalCreatedEvent>
{
    public AnimalCreatedEvent Map(AnimalCreatedExternalDto source) => new()
    {
        AnimalId = source.ExternalAnimalId,
        Name = source.ExternalName
    };

    public AnimalCreatedExternalDto MapBack(AnimalCreatedEvent destination) => new()
    {
        ExternalAnimalId = destination.AnimalId,
        ExternalName = destination.Name
    };
}
