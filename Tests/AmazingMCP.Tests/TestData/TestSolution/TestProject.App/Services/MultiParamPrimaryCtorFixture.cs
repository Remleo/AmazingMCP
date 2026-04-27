using TestProject.Core.Persistence;
using TestProject.Core.Services;

namespace TestProject.App.Services;

/// <summary>
/// Fixture with a multi-line primary constructor for testing that
/// TypeAsParameter usages span the entire parameter list section.
/// </summary>
public sealed class MultiParamPrimaryCtorFixture(
    IAnimalRepository repository,
    IAnimalService animalService,
    INotificationService notificationService)
{
    public void DoWork()
    {
        var count = repository.Count;
        notificationService.Notify($"Count: {count}");
    }
}
