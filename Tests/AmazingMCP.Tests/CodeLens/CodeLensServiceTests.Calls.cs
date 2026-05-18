using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.CodeLens;

public partial class CodeLensServiceTests
{
    // ── Calls ─────────────────────────────────────────────────────────────
    // CodeLensTestFixture line map:
    //   28-31: GetById body — _repository.FindById(id)
    //   43-54: Add body — _repository.FindById, _notification.Notify x2, _repository.Save

    [Test]
    public async Task AnalyzeAsync_MethodCall_AppearsWithCallPrefix()
    {
        // arrange: lines 28-31 — _repository.FindById(id)
        // act
        var result = await Act(28, 31);

        // assert
        result.Should().Contain("call `");
        result.Should().Contain("FindById(");
    }

    [Test]
    public async Task AnalyzeAsync_MethodCall_ShowsFullSignatureWithReturnType()
    {
        // arrange: lines 28-31 — FindById returns Animal?
        // act
        var result = await Act(28, 31);

        // assert: C# style — return type before name, param type before name
        result.Should().Contain("call `TestProject.Core.Models.Animal FindById(int id)`");
        result.Should().Contain("from `TestProject.Core.Persistence.IAnimalRepository`");
    }

    [Test]
    public async Task AnalyzeAsync_MethodCall_VoidReturnType_Shown()
    {
        // arrange: lines 43-54 — Notify returns void
        // act
        var result = await Act(43, 54);

        // assert: void shown in call signature
        result.Should().Contain("call `void Notify(string message)`");
    }

    [Test]
    public async Task AnalyzeAsync_MethodCall_ShowsParamNamesFromDefinition()
    {
        // arrange: lines 43-54 — Save(Animal animal)
        // act
        var result = await Act(43, 54);

        // assert
        result.Should().Contain("call `void Save(TestProject.Core.Models.Animal animal)`");    }

    [Test]
    public async Task AnalyzeAsync_MethodCallsDeduplicatedBySignature()
    {
        // arrange: lines 43-54 — Notify called twice with same signature
        // act
        var result = await Act(43, 54);

        // assert: Notify appears only once
        CountOccurrences(result, "call `void Notify(").Should().Be(1);
    }

    [Test]
    public async Task AnalyzeAsync_MethodCall_ShowsFromClause_WhenExternalClass()
    {
        // arrange: lines 28-31 — FindById declared on IAnimalRepository
        // act
        var result = await Act(28, 31);

        // assert
        result.Should().Contain("from `TestProject.Core.Persistence.IAnimalRepository`");
    }

    [Test]
    public async Task AnalyzeAsync_MethodCall_NoFromClause_WhenSameClass()
    {
        // arrange: all calls in fixture go to external types
        // verify "from" never points to the fixture itself
        // act
        var result = await Act(28, 31);

        // assert
        result.Should().NotContain("from `TestProject.App.Helpers.CodeLensTestFixture`");
    }

    [Test]
    public async Task AnalyzeAsync_SystemMethodCalls_NotIncluded()
    {
        // arrange: lines 43-54 — string interpolation involves System methods
        // act
        var result = await Act(43, 54);

        // assert
        result.Should().NotContain("from `System.");
    }
}
