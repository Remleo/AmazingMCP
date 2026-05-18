using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsProtectedMembers : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedConst_ReturnsProtectedConst()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+const\s+int\s+ProtectedMaxAge\s*=\s*99");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedStaticField_ReturnsProtectedStaticField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+static\s+readonly\s+int\s+ProtectedStaticSeed");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInstanceField_ReturnsProtectedField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+readonly\s+int\s+ProtectedRetryLimit");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedConstructor_ReturnsProtectedConstructor()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+AnimalDefaults\(string displayLabel, bool isProtected\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedStaticProperty_ReturnsProtectedStaticProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+static\s+int\s+ProtectedStaticProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInstanceProperty_ReturnsProtectedProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+int\s+ProtectedInstanceProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedStaticMethod_ReturnsProtectedStaticMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+static\s+string\s+ProtectedStaticFormat");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInstanceMethod_ReturnsProtectedMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+string\s+ProtectedInstanceMethod");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedNestedType_ReturnsProtectedNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("ProtectedInnerConfig");
        result.Should().MatchRegex(@"protected\s+class\s+.*ProtectedInnerConfig");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInternalField_ReturnsProtectedInternalField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected internal\s+string\s+ProtectedInternalTag");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPrivateProtectedField_ReturnsPrivateProtectedField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"private protected\s+int\s+PrivateProtectedScore");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPrivateField_DoesNotReturnPrivateField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert — private fields are not shown; nested type members are not expanded
        result.Should().NotContain("_privateField");
        result.Should().NotContain("Secret");
    }
}
