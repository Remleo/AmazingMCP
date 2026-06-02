using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsFields : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_TypeWithConstants_ReturnsConstants()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("MaxNameLength = 100");
        result.Should().Contain("DefaultPrefix = \"Animal_\"");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithInternalConst_ReturnsInternalConst()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("InternalBatchSize = 50");
        result.Should().MatchRegex(@"internal\s+const\s+int\s+InternalBatchSize");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithStaticField_ReturnsStaticFields()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("FallbackKind");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithPublicInstanceField_ReturnsFields()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("DisplayLabel");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithReadonlyInstanceField_ShowsReadonlyModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"readonly\s+int\s+MaxRetries");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithInternalInstanceField_ReturnsInternalField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+string\s+InternalTag");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithPrivateInstanceField_DoesNotReturnPrivateField()
    {
        // act
        var result = await Act("TestProject.App.Helpers.AnimalFormatter");

        // assert
        result.Should().NotContain("_repository");
    }

    [Test]
    public async Task GetTypeDetailsAsync_TypeWithEvent_ReturnsEvent()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("LabelChanged");
        result.Should().MatchRegex(@"public\s+event\s+EventHandler\??\s+LabelChanged");
    }
}
