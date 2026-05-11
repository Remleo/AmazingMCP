namespace AmazingMCP.Services.Workspace;

public interface ISolutionWatcher : IDisposable
{
    void Start(string solutionPath, Action<string> onSourceChanged, Action<string> onProjectChanged);
    void Stop(string solutionPath);
}
