namespace TestProject.Core.Logging;

/// <summary>
/// Generic tracer interface — used to test closed→open generic collapsing.
/// IGenericTracer&lt;TService&gt; is the open generic.
/// Each service gets its own closed variant: IGenericTracer&lt;FooService&gt;, etc.
/// </summary>
public interface IGenericTracer<TService>
{
    IDisposable Trace(string operation);
}
