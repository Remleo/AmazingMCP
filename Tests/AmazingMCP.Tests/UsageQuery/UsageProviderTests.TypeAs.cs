using AmazingMCP.Models.UsageQuery;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class UsageProviderTestsTypeAs : UsageProviderTestsBase
{
    // ── Parameter ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_Parameter_FindsAnimalInMethodParameter()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.Parameter");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.Parameter);
            m.Entry.TypeName.Should().Contain("Animal");
        });
    }

    [Test]
    public async Task QueryAsync_Parameter_PrimaryConstructor_MultiLine_SpansEntireParameterList()
    {
        // arrange — MultiParamPrimaryCtorFixture has a multi-line primary constructor
        // with IAnimalRepository, IAnimalService, INotificationService as parameters.
        // When IAnimalRepository is matched, the section should span all parameter lines.

        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.Parameter",
            scanInclude: ["TestProject.App.Services.MultiParamPrimaryCtorFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        var match = matches.First(m => m.Scope.MethodName == ".ctor");

        // The parameter list spans multiple lines — section must cover all of them
        match.Scope.Section.EndLine.Should().BeGreaterThan(match.Scope.Section.StartLine,
            "primary constructor parameter list spans multiple lines");
    }

    [Test]
    public async Task QueryAsync_Parameter_PrimaryConstructor_IsFound()
    {
        // act — UsageQueryTestFixture has IAnimalRepository as primary constructor parameter
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.Parameter",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == ".ctor");
    }

    // ── GenericArgument ─────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_GenericArgument_FindsAnimalAsTypeArgument()
    {
        // act — typeName matches Animal, predicate filters to generic argument usages
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.GenericArgument");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.GenericArgument);
            m.Entry.TypeName.Should().Contain("Animal");
        });
    }

    // ── GenericConstraint ───────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_GenericConstraint_FindsWhereConstraint()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.GenericConstraint");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("GenericUsageFixture") &&
            m.Scope.MethodName == "ProcessAnimal");
    }

    // ── ReturnType ──────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ReturnType_FindsMethodsReturningAnimal()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.ReturnType");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.TypeName.Contains("UsageQueryTestFixture"));
    }

    // ── TypeName always populated ─────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_AllMatches_HaveNonEmptyTypeName()
    {
        // act
        var matches = await Act("TestProject.Core.Models.Animal");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.TypeName.Should().NotBeNullOrEmpty());
    }
}