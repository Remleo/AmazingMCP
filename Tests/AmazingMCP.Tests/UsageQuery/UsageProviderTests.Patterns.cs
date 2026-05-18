using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class UsageProviderTestsPatterns : UsageProviderTestsBase
{
    // ── using static ──────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_UsingStatic_MethodCall_IsFound()
    {
        // act — AnimalDefaults.BuildDefaultName called (static method)
        var matches = await Act(
            "TestProject.Core.Models.AnimalDefaults",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"BuildDefaultName\"",
            scanInclude: ["TestProject.App.Services.UsingStaticFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetDefaultName");
    }

    [Test]
    public async Task QueryAsync_UsingStatic_FieldRead_IsFound()
    {
        // act — AnimalDefaults.MaxNameLength (const field)
        var matches = await Act(
            "TestProject.Core.Models.AnimalDefaults",
            predicate: "x.Kind == UsageKind.FieldRead && x.FieldName == \"MaxNameLength\"",
            scanInclude: ["TestProject.App.Services.UsingStaticFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetMaxLength");
    }

    [Test]
    public async Task QueryAsync_UsingStatic_PropertyRead_IsFound()
    {
        // act — AnimalDefaults.MaxAllowed (static property)
        var matches = await Act(
            "TestProject.Core.Models.AnimalDefaults",
            predicate: "x.Kind == UsageKind.PropertyRead && x.PropertyName == \"MaxAllowed\"",
            scanInclude: ["TestProject.App.Services.UsingStaticFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetMaxAllowed");
    }

    // ── is / as pattern ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_IsPattern_SimpleType_IsFound()
    {
        // act — obj is Animal
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.IsOrAs",
            scanInclude: ["TestProject.App.Services.IsAsPatternFixture"]);

        // assert — IsAnimal uses "obj is Animal"
        matches.Should().Contain(m => m.Scope.MethodName == "IsAnimal");
    }

    [Test]
    public async Task QueryAsync_IsPattern_DeclarationPattern_IsFound()
    {
        // act — obj is Animal a
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            scanInclude: ["TestProject.App.Services.IsAsPatternFixture"]);

        // assert — GetAnimalName uses "obj is Animal a"
        matches.Should().Contain(m => m.Scope.MethodName == "GetAnimalName");
    }

    [Test]
    public async Task QueryAsync_AsExpression_IsFound()
    {
        // act — obj as Animal
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.IsOrAs",
            scanInclude: ["TestProject.App.Services.IsAsPatternFixture"]);

        // assert — AsAnimal uses "obj as Animal"
        matches.Should().Contain(m => m.Scope.MethodName == "AsAnimal");
    }
}
