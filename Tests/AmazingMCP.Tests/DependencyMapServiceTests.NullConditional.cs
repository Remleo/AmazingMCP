using AmazingMCP.Models;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Null-conditional invocations (obj?.Method())

    [Test]
    public async Task BuildMapAsync_NullConditionalInstanceCall_DetectedAsDependency()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.TracedAnimalService"];
        impl.Dependencies.Should().Contain(d => d.AbstractionFullName == "TestProject.Core.Logging.IOptionalTracer");
    }

    [Test]
    public async Task BuildMapAsync_NullConditionalInstanceCall_DetectedAsMethodCallUsage()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.TracedAnimalService"];
        var tracerDep = impl.Dependencies.FirstOrDefault(d => d.AbstractionFullName == "TestProject.Core.Logging.IOptionalTracer");
        tracerDep.Should().NotBeNull();
        tracerDep!.Usages.Should().Contain(u => u.MemberName == "StartTrace" && u.Kind == MemberUsageKind.MethodCall);
    }

    [Test]
    public async Task BuildMapAsync_NullConditionalExtensionCall_DetectedAsDependency()
    {
        var result = await Act();

        // tracer?.TraceOperation(...) is an extension method on IOptionalTracer? — receiver type is IOptionalTracer
        var impl = result.Implementations["TestProject.App.Services.TracedAnimalService"];
        var tracerDep = impl.Dependencies.FirstOrDefault(d => d.AbstractionFullName == "TestProject.Core.Logging.IOptionalTracer");
        tracerDep.Should().NotBeNull();
        tracerDep!.Usages.Should().Contain(u => u.MemberName == "TraceOperation" && u.Kind == MemberUsageKind.MethodCall);
    }

    #endregion
}
