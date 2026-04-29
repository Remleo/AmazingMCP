using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Helpers;

/// <summary>
/// Fixture used exclusively by CodeLensServiceTests to verify primary constructor support.
/// </summary>
public sealed class PrimaryCtorTestFixture(
    IAnimalRepository animalRepository,
    INotificationService notificationService) : IAnimalService
{
    public TestProject.Core.Models.Animal? GetById(int id) => animalRepository.FindById(id);

    public System.Collections.Generic.IReadOnlyList<TestProject.Core.Models.Animal> GetByKind(
        TestProject.Core.Models.AnimalKind kind) => animalRepository.FindByKind(kind);

    public void Add(TestProject.Core.Models.Animal animal) => animalRepository.Save(animal);

    public TestProject.Core.Models.Animal? FindOrDefault(int id) => animalRepository.FindById(id);
}
