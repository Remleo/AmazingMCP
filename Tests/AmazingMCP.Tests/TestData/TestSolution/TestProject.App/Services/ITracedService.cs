namespace TestProject.App.Services;

/// <summary>
/// Abstraction for traced services — ensures TracedServiceA/B appear in
/// TestProject.App.Services namespace group for GetProjectDesignDetailsTool tests.
/// </summary>
public interface ITracedService
{
    void Execute();
}
