using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsMisc : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_SourceType_ContainsSourceFileInfo()
    {
        // act
        var result = await Act("TestProject.Core.Models.Animal");

        // assert
        result.Should().Contain("source:");
        result.Should().Contain("Animal.cs");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ExtensionMethod_ShowsThisParameter()
    {
        // act
        var result = await Act("TestProject.App.Helpers.AnimalExtensions");

        // assert
        result.Should().MatchRegex(@"FormatLabel\(this Animal animal, string prefix\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ExtensionMethod_AllMethodsShowThisParameter()
    {
        // act
        var result = await Act("TestProject.App.Helpers.AnimalExtensions");

        // assert
        result.Should().MatchRegex(@"FormatLabel\(this Animal animal, string prefix\)");
        result.Should().MatchRegex(@"IsOfKind\(this Animal animal, AnimalKind kind\)");
        result.Should().MatchRegex(@"WithName\(this Animal animal, string newName\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Interface_ContainsKnownImplementors()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().Contain("Known implementors");
        result.Should().Contain("TestProject.App.Services.AnimalService");
        result.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
        result.Should().Contain("TestProject.App.Services.TracedAnimalService");
    }

    [Test]
    public async Task GetTypeDetailsAsync_AbstractClass_ContainsKnownDerivedTypes()
    {
        // act
        var result = await Act("TestProject.App.Services.AnimalServiceBase");

        // assert
        result.Should().Contain("Known derived types");
        result.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
    }

    [Test]
    public async Task GetTypeDetailsAsync_InterfaceWithNoImplementors_DoesNotContainDerivedSection()
    {
        // act — IAnimalValidator has no implementors in the test solution
        var result = await Act("TestProject.Core.Services.IAnimalValidator");

        // assert
        result.Should().NotContain("Known implementors");
        result.Should().NotContain("Known derived types");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Interface_KnownImplementors_ContainSourceFileInfo()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert — each implementor line should have "// source:" and the filename
        var implementorsIndex = result.IndexOf("Known implementors", StringComparison.Ordinal);
        implementorsIndex.Should().BeGreaterThanOrEqualTo(0);
        var afterImplementors = result[implementorsIndex..];
        afterImplementors.Should().Contain("AnimalService // source:");
        afterImplementors.Should().Contain("AnimalService.cs");
    }

    [Test]
    public async Task GetTypeDetailsAsync_AbstractClass_KnownDerivedTypes_ContainSourceFileInfo()
    {
        // act
        var result = await Act("TestProject.App.Services.AnimalServiceBase");

        // assert — derived type line should have "// source:" and the filename
        var derivedIndex = result.IndexOf("Known derived types", StringComparison.Ordinal);
        derivedIndex.Should().BeGreaterThanOrEqualTo(0);
        var afterDerived = result[derivedIndex..];
        afterDerived.Should().Contain("AdvancedAnimalService // source:");
        afterDerived.Should().Contain("AdvancedAnimalService.cs");
    }
}
