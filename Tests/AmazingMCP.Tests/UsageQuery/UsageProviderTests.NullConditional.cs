using AmazingMCP.Models.UsageQuery;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class UsageProviderTestsNullConditional : UsageProviderTestsBase
{
    // tracer?.StartTrace(...) — direct null-conditional instance call
    [Test]
    public async Task QueryAsync_NullConditional_InstanceMethodCall_IsFound()
    {
        var matches = await Act(
            "TestProject.Core.Logging.IOptionalTracer",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"StartTrace\"",
            scanInclude: ["TestProject.App.Services.TracedAnimalService"]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetById");
    }

    // tracer?.TraceOperation(...) — extension method via null-conditional
    [Test]
    public async Task QueryAsync_NullConditional_ExtensionMethodCall_IsFound()
    {
        var matches = await Act(
            "TestProject.Core.Logging.IOptionalTracer",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"TraceOperation\"",
            scanInclude: ["TestProject.App.Services.TracedAnimalService"]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetByKind");
    }
}
