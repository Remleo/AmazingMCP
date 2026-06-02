using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsNestedTypes : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_TypeWithPublicNestedType_ReturnsNestedTypes()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("ValidationRules");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithInternalNestedType_ReturnsInternalNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("CacheOptions");
        result.Should().MatchRegex(@"internal\s+class\s+.*CacheOptions");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithPrivateNestedType_ReturnsPrivateNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert — source types show all nested types including private
        result.Should().Contain("PrivateInner");
        result.Should().MatchRegex(@"private\s+class\s+.*PrivateInner");
    }

    [Test]
    public async Task GetTypeDetailsAsync_NestedType_HasNestedMarker()
    {
        // act — query the nested type directly
        var result = await Act("TestProject.Core.Models.AnimalDefaults.ValidationRules");

        // assert — the type header is prefixed with /* nested */
        result.Should().MatchRegex(@"/\* nested \*/\s+public\s+class\s+.*ValidationRules");
    }
}
