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
    [Test]
    public async Task QueryAsync_SelfReference_FieldAccess_IsNotReturned()
    {
        // arrange — UsageQueryTestFixture accesses its own _defaultAnimal field internally.
        // Searching for UsageQueryTestFixture usages should NOT return self-references.

        // act
        var matches = await Act(
            "TestProject.App.Services.UsageQueryTestFixture",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — no matches where the scope type equals the target type
        // for field/property access (self-references like accessing own fields should be excluded).
        // MethodCall is intentionally excluded from this check — implicit-this calls are valid usages.
        var selfRefs = matches.Where(m =>
            m.Entry.Kind is UsageKind.FieldRead or UsageKind.FieldWrite
                         or UsageKind.PropertyRead or UsageKind.PropertyWrite
            && m.Entry.TypeName == "TestProject.App.Services.UsageQueryTestFixture").ToList();

        selfRefs.Should().BeEmpty();
    }

    // ── Large block suppression ───────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_UsageInsideLargeBlock_SectionIsSingleLine()
    {
        // arrange — UsageInsideLargeLambda has a Save() call inside a lambda block >5 lines.
        // The section should be the single line of the call, not the entire lambda.

        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"Save\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — the match inside the large lambda should have a 1-line section
        var lambdaMatch = matches.FirstOrDefault(m =>
            m.Scope.MethodName == "UsageInsideLargeLambda");

        lambdaMatch.Should().NotBeNull("Save() inside large lambda should be found");
        lambdaMatch!.Scope.Section.StartLine.Should().Be(lambdaMatch.Scope.Section.EndLine,
            "usage inside a large block should fall back to a single-line section");
    }

    [Test]
    public async Task QueryAsync_UsageOutsideLargeBlock_SectionIsNormal()
    {
        // arrange — SaveIfNotFull has Save() inside a short if-body (not a large block).
        // The section resolves normally via SectionResolver (InvocationExpression),
        // not suppressed to fallback. Both should be single-line — the key difference
        // is that the large-lambda match is suppressed while this one resolves naturally.

        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"Save\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — both matches are found
        matches.Should().Contain(m => m.Scope.MethodName == "SaveIfNotFull",
            "Save() in SaveIfNotFull should be found");
        matches.Should().Contain(m => m.Scope.MethodName == "UsageInsideLargeLambda",
            "Save() inside large lambda should also be found");
    }

    // ── TypeName filter (typeName) ─────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypePattern_FiltersToMatchingType()
    {
        // act — only usages of IAnimalRepository
        var matches = await Act("TestProject.Core.Persistence.IAnimalRepository");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.TypeName.Should().Be("TestProject.Core.Persistence.IAnimalRepository"));
    }

    [Test]
    public async Task QueryAsync_TypePattern_NoMatch_ReturnsEmpty()
    {
        // act
        var matches = await Act("TestProject.Core.Persistence.NonExistentType99");

        // assert
        matches.Should().BeEmpty();
    }

    // ── MethodCall ────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_MethodCall_FindsCallsByName()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.Should().BeEquivalentTo(new
            {
                Kind = UsageKind.MethodCall,
                MethodName = "FindById"
            }));
    }

    [Test]
    public async Task QueryAsync_MethodCall_MatchContainsCorrectScope()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"");

        // assert — at least one match is inside UsageQueryTestFixture.FindAnimalById
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "FindAnimalById");
    }

    // ── PropertyRead ──────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyRead_FindsReadsByName()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.PropertyRead && x.PropertyName == \"Count\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.Should().BeEquivalentTo(new { Kind = UsageKind.PropertyRead, PropertyName = "Count" }));
    }

    // ── PropertyWrite ─────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyWrite_FindsWritesByName()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.PropertyWrite && x.PropertyName == \"Name\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "RenameAnimal");
    }

    // ── ConstructorCall ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ConstructorCall_FindsNewExpressionsByTypeName()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.ConstructorCall && x.MethodName == \"Animal\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.Should().BeEquivalentTo(new { Kind = UsageKind.ConstructorCall, MethodName = "Animal" }));
    }

    [Test]
    public async Task QueryAsync_ConstructorCall_MatchIsInsideCreateAnimalMethod()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.ConstructorCall && x.MethodName == \"Animal\"");

        // assert
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "CreateAnimal");
    }
}