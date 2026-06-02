using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsVirtualAndAbstract : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_TypeWithVirtualMethod_ShowsVirtualModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"virtual\s+string\s+GetLabel\(\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithVirtualMethodWithParams_ShowsVirtualModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"virtual\s+int\s+ComputeScore\(int input\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithProtectedVirtualMethod_ShowsBothModifiers()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+virtual\s+string\s+FormatInternal\(\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithVirtualProperty_ShowsVirtualModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"virtual\s+int\s+VirtualProp");
    }

    [Test]
    public async Task GetTypeDetailsAsync_AbstractClass_ShowsAbstractMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"abstract\s+string\s+GetName\(\)");
        result.Should().MatchRegex(@"abstract\s+int\s+GetScore\(int input\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_AbstractClass_ShowsProtectedAbstractMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"protected\s+abstract\s+string\s+FormatDescription\(\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_AbstractClass_ShowsAbstractProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"abstract\s+int\s+AbstractProp");
    }

    [Test]
    public async Task GetTypeDetailsAsync_AbstractClass_ShowsVirtualMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"virtual\s+string\s+GetSummary\(\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ConcreteClass_ShowsOverrideMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"override\s+string\s+GetName\(\)");
        result.Should().MatchRegex(@"override\s+int\s+GetScore\(int input\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ConcreteClass_ShowsSealedOverrideMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"public\s+override\s+sealed\s+string\s+GetSummary\(\)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ConcreteClass_ShowsOverrideProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"override\s+int\s+AbstractProp");
        result.Should().MatchRegex(@"override\s+string\s+VirtualPropOnBase");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ConcreteClass_ShowsProtectedOverrideMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"protected\s+override\s+string\s+FormatDescription\(\)");
    }
}
