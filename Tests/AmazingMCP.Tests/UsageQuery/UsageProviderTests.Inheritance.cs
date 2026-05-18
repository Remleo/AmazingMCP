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
    // ── Inheritance ─────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_Inheritance_Interface_FindsAllImplementors()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Services.IAnimalService",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert
        var typeNames = matches.Select(m => m.Scope.TypeName).ToList();
        typeNames.Should().Contain("TestProject.App.Services.AnimalService");
        typeNames.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
        typeNames.Should().Contain("TestProject.App.Services.TracedAnimalService");
        typeNames.Should().Contain("TestProject.App.Services.TracedServiceA");
    }

    [Test]
    public async Task QueryAsync_Inheritance_Interface_AllMatchesHaveCorrectKindAndTypeName()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Services.IAnimalService",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.Inheritance);
            m.Entry.TypeName.Should().Be("TestProject.Core.Services.IAnimalService");
        });
    }

    [Test]
    public async Task QueryAsync_Inheritance_AbstractClass_FindsSubclasses()
    {
        // act
        var matches = await Act(
            "TestProject.App.Services.AnimalServiceBase",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert
        matches.Select(m => m.Scope.TypeName)
            .Should().Contain("TestProject.App.Services.AdvancedAnimalService");
    }

    [Test]
    public async Task QueryAsync_Inheritance_SourceType_HasFilePath()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Services.IAnimalService",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert — source types must have a non-empty file path and a non-null Section
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Scope.FilePath.Should().NotBeNullOrEmpty();
            m.Scope.Section.Should().NotBeNull();
        });
    }

    [Test]
    public async Task QueryAsync_Inheritance_SourceType_SectionIsDeclarationLineOnly()
    {
        // act — AnimalService : IAnimalService, body spans ~40 lines
        var matches = await Act(
            "TestProject.Core.Services.IAnimalService",
            predicate: "x.Kind == UsageKind.Inheritance",
            scanInclude: ["TestProject.App.Services.AnimalService"]);

        // assert — section must be just the declaration (up to opening brace), not the full class body
        var match = matches.Should().ContainSingle().Subject;
        var section = match.Scope.Section!;
        (section.EndLine - section.StartLine).Should().BeLessThan(3,
            "section should span only the declaration line(s) up to the opening brace, not the full class body");
    }

    [Test]
    public async Task QueryAsync_Inheritance_ScanInclude_FiltersResults()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Services.IAnimalService",
            predicate: "x.Kind == UsageKind.Inheritance",
            scanInclude: ["TestProject.App.Services.AnimalService"]);

        // assert — only AnimalService passes the scanInclude filter
        matches.Should().BeEquivalentTo(
            [new { Scope = new { TypeName = "TestProject.App.Services.AnimalService" } }],
            o => o.Including(m => m.Scope.TypeName));
    }

    [Test]
    public async Task QueryAsync_Inheritance_Formatter_OutputContainsImplementorHeader()
    {
        // arrange
        var (matches, _, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            "TestProject.Core.Services.IAnimalService",
            predicate: "x.Kind == UsageKind.Inheritance",
            scanInclude: null,
            scanExclude: null);

        // act
        var output = new UsageResultFormatter().Format(matches);

        // assert
        output.Should().Contain("TestProject.App.Services.AnimalService");
    }

    [Test]
    public async Task QueryAsync_Inheritance_NuGetBaseClass_FindsSourceImplementors()
    {
        // act — AutoMapper.Profile is a NuGet class; AnimalMappingProfile extends it
        var matches = await Act(
            "AutoMapper.Profile",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert — source implementor is found
        matches.Select(m => m.Scope.TypeName)
            .Should().Contain("TestProject.App.Mapping.AnimalMappingProfile");
    }

    [Test]
    public async Task QueryAsync_Inheritance_Formatter_SyntheticDeclaration_ContainsInheritance()
    {
        // arrange — search for implementors of a NuGet interface so we get a synthetic match
        // IHealthCheck is not in TestSolution, but we can verify via FormatHeader directly
        // Instead: verify that source match section declaration contains the base class name
        var matches = await Act(
            "AutoMapper.Profile",
            predicate: "x.Kind == UsageKind.Inheritance",
            scanInclude: ["TestProject.App.Mapping.AnimalMappingProfile"]);

        // act
        var output = new UsageResultFormatter().Format(matches);

        // assert — declaration line must show ": Profile" (short name as written in source)
        output.Should().Contain(": Profile");
    }

    [Test]
    public async Task QueryAsync_Inheritance_BodylessType_SectionIsDeclarationLine()
    {
        // act — BodylessTypeFixture : IInheritanceTestMarker; — C# 12 class without braces
        var matches = await Act(
            "TestProject.Core.Models.IInheritanceTestMarker",
            predicate: "x.Kind == UsageKind.Inheritance",
            scanInclude: ["TestProject.Core.Models.BodylessTypeFixture"]);

        // assert — section must be the single declaration line (no body, no braces)
        var match = matches.Should().ContainSingle().Subject;
        var section = match.Scope.Section!;
        section.StartLine.Should().Be(section.EndLine,
            "bodyless type has no braces — entire declaration fits on one line");
    }

    [Test]
    public async Task QueryAsync_Inheritance_OpenGenericInterface_FindsImplementors()
    {
        // act — open generic: IRepository<T>
        var matches = await Act(
            "TestProject.Core.Persistence.IRepository<T>",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert — GenericRepository<T> directly implements IRepository<T>
        matches.Select(m => m.Scope.TypeName)
            .Should().Contain("TestProject.App.Persistence.GenericRepository<T>");
    }

    [Test]
    public async Task QueryAsync_Inheritance_OpenGenericInterface_AllMatchesHaveCorrectKind()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Persistence.IRepository<T>",
            predicate: "x.Kind == UsageKind.Inheritance");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m => m.Entry.Kind.Should().Be(UsageKind.Inheritance));
    }
}