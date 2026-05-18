using AmazingMCP.Models.UsageQuery;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class UsageProviderTestsNameOfTypeOf : UsageProviderTestsBase
{
    // ── TypeOf ──────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeOf_FindsTypeofAnimal()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeOf",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.TypeOf);
            m.Entry.TypeName.Should().Be("TestProject.Core.Models.Animal");
        });
    }

    [Test]
    public async Task QueryAsync_TypeOf_FindsTypeofIAnimalRepository()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.TypeOf",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetRepositoryType");
    }

    [Test]
    public async Task QueryAsync_TypeOf_OpenGeneric_FindsListT()
    {
        // act — typeof(List<>) produces unbound generic System.Collections.Generic.List<>
        var matches = await Act(
            "System.Collections.Generic.List<>",
            predicate: "x.Kind == UsageKind.TypeOf",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetOpenGenericListType");
    }

    // ── NameOf ──────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_NameOf_ImplicitThis_Method_IsFound()
    {
        // act — nameof(GetThisMethodName) — implicit this, method of current class
        var matches = await Act(
            "TestProject.App.Services.UsageQueryTestFixture",
            predicate: "x.Kind == UsageKind.NameOf && x.MethodName == \"GetThisMethodName\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.MethodName == "GetThisMethodName" &&
            m.Scope.MethodName == "GetThisMethodName");
    }

    [Test]
    public async Task QueryAsync_NameOf_ImplicitThis_Type_IsFound()
    {
        // act — nameof(UsageQueryTestFixture) — type name without qualifier
        var matches = await Act(
            "TestProject.App.Services.UsageQueryTestFixture",
            predicate: "x.Kind == UsageKind.NameOf",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — GetThisTypeName uses nameof(UsageQueryTestFixture)
        matches.Should().Contain(m => m.Scope.MethodName == "GetThisTypeName");
    }

    [Test]
    public async Task QueryAsync_NameOf_InMethodParamAttribute_SectionSpansMethodDeclaration()
    {
        // act — [DefaultValue(nameof(Animal))] on a regular method parameter
        // Section should span the method declaration, not just the attribute line
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf",
            scanInclude: ["TestProject.App.Services.NameOfInMethodAttributeFixture"]);

        // assert — match from ProcessWithAttributedParam must span more than one line
        var paramMatch = matches.Should().Contain(m => m.Scope.MethodName == "ProcessWithAttributedParam").Subject;
        (paramMatch.Scope.Section!.EndLine - paramMatch.Scope.Section.StartLine).Should().BeGreaterThan(0,
            "section should span the method declaration, not just the attribute line on the parameter");
    }

    [Test]
    public async Task QueryAsync_NameOf_InMethodAttribute_SectionSpansMethodDeclaration()
    {
        // act — [DisplayName(nameof(Animal))] on a method
        // Section should span the method declaration, not just the attribute line
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf",
            scanInclude: ["TestProject.App.Services.NameOfInMethodAttributeFixture"]);

        // assert — section must include the method declaration line (attribute + method signature)
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            (m.Scope.Section!.EndLine - m.Scope.Section.StartLine).Should().BeGreaterThan(0,
                "section should span attribute + method declaration, not just the attribute line"));
    }

    [Test]
    public async Task QueryAsync_NameOf_InPrimaryCtorParamAttribute_SectionSpansParameterList()
    {
        // act — [DefaultValue(nameof(Animal))] on a primary constructor parameter
        // The section should span the entire parameter list (declaration context), not just the attribute line
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf",
            scanInclude: ["TestProject.App.Services.NameOfInPrimaryCtorParamAttributeFixture"]);

        // assert — section must span more than one line (the full parameter list)
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            (m.Scope.Section!.EndLine - m.Scope.Section.StartLine).Should().BeGreaterThan(0,
                "section should span the full parameter list, not just the attribute line"));
    }

    [Test]
    public async Task QueryAsync_NameOf_InAttribute_IsFound()
    {
        // act — [DisplayName(nameof(Animal))] on NameOfInAttributeFixture
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf",
            scanInclude: ["TestProject.App.Services.NameOfInAttributeFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m => m.Entry.TypeName.Should().Be("TestProject.Core.Models.Animal"));
    }

    [Test]
    public async Task QueryAsync_NameOf_TypeOnly_FindsNameofAnimal()
    {
        // act — nameof(Animal)
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetAnimalTypeName");
    }

    [Test]
    public async Task QueryAsync_NameOf_Member_FindsNameofAnimalName()
    {
        // act — nameof(Animal.Name)
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf && x.PropertyName == \"Name\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.PropertyName == "Name" &&
            m.Scope.MethodName == "GetAnimalNamePropertyName");
    }

    [Test]
    public async Task QueryAsync_NameOf_Member_FindsNameofAnimalKind()
    {
        // act — nameof(Animal.Kind)
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.NameOf && x.PropertyName == \"Kind\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.PropertyName == "Kind" &&
            m.Scope.MethodName == "GetKindPropertyName");
    }
}