using TestProject.Core.Logging;

namespace TestProject.App.Services;

/// <summary>
/// Implementation of IUnusedLogger — exists so the interface appears in abstractions,
/// but nobody calls Log() on it, so "Used by" section must be empty.
/// </summary>
public class UnusedLoggerImpl : IUnusedLogger
{
    public void Log(string message) { }
}
