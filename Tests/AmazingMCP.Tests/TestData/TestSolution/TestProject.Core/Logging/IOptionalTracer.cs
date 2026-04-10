namespace TestProject.Core.Logging;

public interface IOptionalTracer
{
    IDisposable StartTrace(string name);
}

public static class OptionalTracerExtensions
{
    public static IDisposable? TraceOperation(this IOptionalTracer? tracer, string name) =>
        tracer?.StartTrace(name);
}
