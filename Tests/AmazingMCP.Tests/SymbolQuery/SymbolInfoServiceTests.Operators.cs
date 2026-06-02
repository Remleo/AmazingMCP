using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsOperators : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_TypeWithImplicitOperator_ReturnsImplicitOperator()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalWeight");

        // assert
        result.Should().MatchRegex(@"implicit operator double\(AnimalWeight");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithExplicitOperator_ReturnsExplicitOperator()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalWeight");

        // assert
        result.Should().MatchRegex(@"explicit operator AnimalWeight\(double");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithUserDefinedOperator_ReturnsOperator()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalWeight");

        // assert
        result.Should().MatchRegex(@"operator \+\(AnimalWeight");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithComparisonOperators_ReturnsBothOperators()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalWeight");

        // assert
        result.Should().MatchRegex(@"operator >\(AnimalWeight");
        result.Should().MatchRegex(@"operator <\(AnimalWeight");
    }
}
