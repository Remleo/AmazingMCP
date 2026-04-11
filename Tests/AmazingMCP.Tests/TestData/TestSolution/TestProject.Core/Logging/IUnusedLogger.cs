namespace TestProject.Core.Logging;

/// <summary>
/// Intentionally unused interface — no class calls its methods.
/// Used to verify that abstractions with no member usages don't appear in "Used by".
/// </summary>
public interface IUnusedLogger
{
    void Log(string message);
}
