using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsEnum : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_UnknownType_ReturnsNotFound()
    {
        // act
        var result = await Act("NonExistent.Type");

        // assert
        result.Should().Contain("not found");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Enum_ReturnsAllValues()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().Contain("Unknown = 0");
        result.Should().Contain("Cat = 1");
        result.Should().Contain("Dog = 2");
        result.Should().Contain("Parrot = 3");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Enum_ReturnsUnderlyingType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().Contain("Underlying type:");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Enum_DoesNotContainMemberSections()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().NotContain("int Id");
        result.Should().NotContain("string Name");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Enum_HeaderContainsEnum()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().MatchRegex(@"public\s+enum\s+TestProject\.Core\.Models\.AnimalKind");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Enum_DoesNotContainDerivedSection()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().NotContain("Known implementors");
        result.Should().NotContain("Known derived types");
    }
}
