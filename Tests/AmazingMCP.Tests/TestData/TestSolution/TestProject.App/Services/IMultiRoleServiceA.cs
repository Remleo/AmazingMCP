namespace TestProject.App.Services;

/// <summary>
/// First of two interfaces implemented by MultiRoleService.
/// Used to test that when both interfaces appear in a query result,
/// the shared implementation's dependencies are printed only once.
/// </summary>
public interface IMultiRoleServiceA
{
    void DoA();
}
