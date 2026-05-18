using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.CodeLens;

public partial class CodeLensServiceTests
{
    // ── Fields ────────────────────────────────────────────────────────────
    // CodeLensTestFixture line map:
    //   16: readonly IAnimalRepository _repository;
    //   17: readonly INotificationService _notification;
    //   28-31: GetById body — accesses _repository
    //   43-54: Add body — accesses _repository and _notification multiple times

    [Test]
    public async Task AnalyzeAsync_FieldAccess_AppearsWithFieldPrefix()
    {
        // arrange: lines 28-31 — GetById body accesses _repository
        // act
        var result = await Act(28, 31);

        // assert
        result.Should().Contain("field `TestProject.Core.Persistence.IAnimalRepository _repository`");
    }

    [Test]
    public async Task AnalyzeAsync_FieldAccess_DeduplicatedByNameAndType()
    {
        // arrange: lines 43-54 — Add body accesses _repository and _notification multiple times
        // act
        var result = await Act(43, 54);

        // assert: each field appears only once
        CountOccurrences(result, "field `TestProject.Core.Persistence.IAnimalRepository _repository`").Should().Be(1);
        CountOccurrences(result, "field `TestProject.Core.Services.INotificationService _notification`").Should().Be(1);
    }

    [Test]
    public async Task AnalyzeAsync_TrivialField_NotIncluded()
    {
        // arrange: lines 28-31 — no trivial-typed fields accessed
        // act
        var result = await Act(28, 31);

        // assert
        result.Should().NotContain("field `int ");
        result.Should().NotContain("field `string ");
    }

    [Test]
    public async Task AnalyzeAsync_ExternalFieldAccess_NotIncluded()
    {
        // arrange: lines 43-54 — animal.Id accessed but Animal is not the current class
        // act
        var result = await Act(43, 54);

        // assert: Id (int, trivial) and Name (string, trivial) don't appear anyway,
        // and Kind (AnimalKind) belongs to Animal not to CodeLensTestFixture — skipped
        result.Should().NotContain("field `TestProject.Core.Models.AnimalKind Kind`");
    }
}
