using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class UsageProviderTests
{
    // ── TypeAsParameter ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsParameter_FindsAnimalInMethodParameter()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeAsParameter");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.TypeAsParameter);
            m.Entry.TypeName.Should().Contain("Animal");
        });
    }

    [Test]
    public async Task QueryAsync_TypeAsParameter_PrimaryConstructor_MultiLine_SpansEntireParameterList()
    {
        // arrange — MultiParamPrimaryCtorFixture has a multi-line primary constructor
        // with IAnimalRepository, IAnimalService, INotificationService as parameters.
        // When IAnimalRepository is matched, the section should span all parameter lines.

        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.TypeAsParameter",
            scanInclude: ["TestProject.App.Services.MultiParamPrimaryCtorFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        var match = matches.First(m => m.Scope.MethodName == ".ctor");

        // The parameter list spans multiple lines — section must cover all of them
        match.Scope.Section.EndLine.Should().BeGreaterThan(match.Scope.Section.StartLine,
            "primary constructor parameter list spans multiple lines");
    }

    [Test]
    public async Task QueryAsync_TypeAsParameter_PrimaryConstructor_IsFound()
    {
        // act — UsageQueryTestFixture has IAnimalRepository as primary constructor parameter
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.TypeAsParameter",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == ".ctor");
    }

    // ── TypeAsGenericArgument ─────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsGenericArgument_FindsAnimalAsTypeArgument()
    {
        // act — typeName matches Animal, predicate filters to generic argument usages
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeAsGenericArgument");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.TypeAsGenericArgument);
            m.Entry.TypeName.Should().Contain("Animal");
        });
    }

    // ── TypeAsGenericConstraint ───────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsGenericConstraint_FindsWhereConstraint()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeAsGenericConstraint");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("GenericUsageFixture") &&
            m.Scope.MethodName == "ProcessAnimal");
    }

    // ── TypeAsReturnType ──────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsReturnType_FindsMethodsReturningAnimal()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeAsReturnType");

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