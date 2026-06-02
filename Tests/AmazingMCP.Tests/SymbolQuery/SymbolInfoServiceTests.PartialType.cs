using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsPartialType : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_PartialType_ContainsBothFileNames()
    {
        // act
        var result = await Act("TestProject.Core.Models.PartialAnimal");

        // assert
        result.Should().Contain("PartialAnimal.cs");
        result.Should().Contain("PartialAnimal.Extra.cs");
    }

    [Test]
    public async Task GetTypeDetailsAsync_PartialType_DoesNotContainLineNumber()
    {
        // act
        var result = await Act("TestProject.Core.Models.PartialAnimal");

        // assert — line number is only shown for single-file types
        var sourceLine = result.Split('\n').First(l => l.Contains("source:"));
        sourceLine.Should().NotMatchRegex(@"line \d+");
    }

    [Test]
    public async Task GetTypeDetailsAsync_PartialType_FilesGroupedUnderSameDirectory()
    {
        // act
        var result = await Act("TestProject.Core.Models.PartialAnimal");

        // assert — both files appear on the same source line (same directory group)
        var sourceLine = result.Split('\n').First(l => l.Contains("source:"));
        sourceLine.Should().Contain("PartialAnimal.cs");
        sourceLine.Should().Contain("PartialAnimal.Extra.cs");
        sourceLine.Should().NotContain("|");
    }

    [Test]
    public async Task GetTypeDetailsAsync_SingleFileType_ContainsLineNumber()
    {
        // act
        var result = await Act("TestProject.Core.Models.Animal");

        // assert
        result.Should().MatchRegex(@"source:.*Animal\.cs, line \d+");
    }
}
