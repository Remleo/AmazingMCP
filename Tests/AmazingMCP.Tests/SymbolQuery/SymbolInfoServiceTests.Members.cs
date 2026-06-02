using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsMembers : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_TypeWithProperties_ReturnsProperties()
    {
        // act
        var result = await Act("TestProject.Core.Models.Animal");

        // assert
        result.Should().Contain("int Id");
        result.Should().Contain("string Name");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithStaticProperty_ReturnsStaticProperties()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("MaxAllowed");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithStaticMethod_ReturnsStaticMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("BuildDefaultName");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithInternalStaticMethod_ReturnsInternalStaticMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+static\s+string\s+InternalFormat");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithInstanceMethod_ReturnsMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("InstanceMethod");
    }

    [Test]
    public async Task GetTypeDetailsAsync_Interface_ReturnsInterfaceMethods()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().Contain("GetById");
        result.Should().Contain("GetByKind");
        result.Should().Contain("Add");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithConstructors_ReturnsConstructorsSection()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("AnimalDefaults()");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithParameterlessConstructor_ShowsEmptyParams()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("AnimalDefaults()");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithParameterizedConstructor_ShowsParams()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("AnimalDefaults(string displayLabel)");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithInternalConstructor_ShowsInternalConstructor()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+AnimalDefaults\(string displayLabel, int maxRetries\)");
    }
}
