using TestProject.Core.Persistence;
using TestProject.Core.Models;

namespace TestProject.App.Services;

/// <summary>
/// Implements both IMultiRoleServiceA and IMultiRoleServiceB with a shared dependency.
/// Used to test that when both interfaces appear in a wildcard query result,
/// the implementation's dependencies are printed in full only once,
/// and subsequent appearances show "*(dependencies listed above)*".
/// </summary>
public class MultiRoleService(IAnimalRepository repository) : IMultiRoleServiceA, IMultiRoleServiceB
{
    public void DoA() => repository.FindById(0);
    public void DoB() => repository.Save(new Animal());
}
