using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsTypeHeaderModifiers : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetSymbolInfoAsync_PublicClass_HeaderContainsPublicClass()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"public\s+class\s+TestProject\.Core\.Models\.AnimalDefaults");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_HeaderContainsAbstractClass()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"public\s+abstract\s+class\s+TestProject\.Core\.Models\.AnimalBase");
    }

    [Test]
    public async Task GetSymbolInfoAsync_SealedClass_HeaderContainsSealedClass()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"public\s+class\s+TestProject\.Core\.Models\.ConcreteAnimal");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Interface_HeaderContainsInterface()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().MatchRegex(@"public\s+interface\s+TestProject\.Core\.Services\.IAnimalService");
    }
}
