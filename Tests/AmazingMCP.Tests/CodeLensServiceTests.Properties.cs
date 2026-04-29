using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class CodeLensServiceTests
{
    // ── Properties ────────────────────────────────────────────────────────
    // CodeLensTestFixture line map:
    //   18: public AnimalKind DefaultKind { get; } = AnimalKind.Unknown;
    //   65: return _repository.FindByKind(DefaultKind);

    [Test]
    public async Task AnalyzeAsync_PropertyAccess_AppearsWithPropPrefix()
    {
        // arrange: line 65 — reads DefaultKind (property of current class)
        // act
        var result = await Act(65, 65);

        // assert
        result.Should().Contain("prop `TestProject.Core.Models.AnimalKind DefaultKind`");
    }

    [Test]
    public async Task AnalyzeAsync_TrivialProperty_NotIncluded()
    {
        // arrange: line 65
        // act
        var result = await Act(65, 65);

        // assert
        result.Should().NotContain("prop `int ");
        result.Should().NotContain("prop `string ");
    }

    [Test]
    public async Task AnalyzeAsync_ExternalObjectProperty_NotIncluded()
    {
        // arrange: lines 43-54 — animal.Kind accessed but Animal is not the current class
        // act
        var result = await Act(43, 54);

        // assert: Animal.Kind belongs to Animal, not to CodeLensTestFixture
        result.Should().NotContain("prop `TestProject.Core.Models.AnimalKind Kind`");
    }
}
