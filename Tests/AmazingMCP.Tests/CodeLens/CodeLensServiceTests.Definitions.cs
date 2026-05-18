using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.CodeLens;

public partial class CodeLensServiceTests
{
    // ── Definitions ───────────────────────────────────────────────────────
    // CodeLensTestFixture line map:
    //   14: public class CodeLensTestFixture : IAnimalService
    //   16: readonly IAnimalRepository _repository;
    //   17: readonly INotificationService _notification;
    //   18: public AnimalKind DefaultKind { get; } = AnimalKind.Unknown;
    //   20-23: constructor(IAnimalRepository, INotificationService)
    //   27: public Animal? GetById(int id)
    //   42: public void Add(Animal animal)

    [Test]
    public async Task AnalyzeAsync_TypeDefinition_AppearsWithDefPrefix()
    {
        // arrange: line 14 — class declaration
        // act
        var result = await Act(14, 14);

        // assert: short name for the class itself
        result.Should().Contain("def `CodeLensTestFixture");
    }

    [Test]
    public async Task AnalyzeAsync_TypeDefinition_ShowsBaseTypesWithFullNames()
    {
        // arrange: line 14 — CodeLensTestFixture : IAnimalService
        // act
        var result = await Act(14, 14);

        // assert: base type uses full name
        result.Should().Contain("TestProject.Core.Services.IAnimalService");
    }

    [Test]
    public async Task AnalyzeAsync_ConstructorDefinition_AppearsWithCtorPrefix()
    {
        // arrange: lines 20-23 — constructor
        // act
        var result = await Act(20, 23);

        // assert: ctor prefix + short class name
        result.Should().Contain("ctor `CodeLensTestFixture(");
    }

    [Test]
    public async Task AnalyzeAsync_ConstructorDefinition_ShowsAllParamTypes()
    {
        // arrange: lines 20-23
        // act
        var result = await Act(20, 23);

        // assert: C# style — type before name
        result.Should().Contain("TestProject.Core.Persistence.IAnimalRepository repository");
        result.Should().Contain("TestProject.Core.Services.INotificationService notification");
    }

    [Test]
    public async Task AnalyzeAsync_MethodDefinition_AppearsWithDefPrefix()
    {
        // arrange: line 27 — "public Animal? GetById(int id)"
        // act
        var result = await Act(27, 27);

        // assert
        result.Should().Contain("def `");
        result.Should().Contain("GetById(");
    }

    [Test]
    public async Task AnalyzeAsync_MethodDefinition_ShowsReturnType()
    {
        // arrange: line 27 — GetById returns Animal?
        // act
        var result = await Act(27, 27);

        // assert: return type before method name
        result.Should().Contain("def `TestProject.Core.Models.Animal GetById(int id)`");
    }

    [Test]
    public async Task AnalyzeAsync_MethodDefinition_VoidReturnType_Shown()
    {
        // arrange: line 42 — "public void Add(Animal animal)"
        // act
        var result = await Act(42, 42);

        // assert: void shown in def
        result.Should().Contain("def `void Add(TestProject.Core.Models.Animal animal)`");
    }

    [Test]
    public async Task AnalyzeAsync_OuterClassDefinition_NotIncluded_WhenRangeIsInsideBody()
    {
        // arrange: lines 28-31 — inside GetById body
        // act
        var result = await Act(28, 31);

        // assert: class def not in output (starts outside range)
        result.Should().NotContain("def `CodeLensTestFixture");
    }

    [Test]
    public async Task AnalyzeAsync_FieldDefinition_AppearsWithFieldPrefix()
    {
        // arrange: line 16
        // act
        var result = await Act(16, 16);

        // assert
        result.Should().Contain("field `TestProject.Core.Persistence.IAnimalRepository _repository`");
    }

    [Test]
    public async Task AnalyzeAsync_PropertyDefinition_AppearsWithPropPrefix()
    {
        // arrange: line 18
        // act
        var result = await Act(18, 18);

        // assert
        result.Should().Contain("prop `TestProject.Core.Models.AnimalKind DefaultKind`");
    }

    [Test]
    public async Task AnalyzeAsync_TrivialFieldDefinition_NotIncluded()
    {
        // arrange: lines 16-17 — both non-trivial interface fields
        // act
        var result = await Act(16, 17);

        // assert
        result.Should().Contain("field `TestProject.Core.Persistence.IAnimalRepository _repository`");
        result.Should().Contain("field `TestProject.Core.Services.INotificationService _notification`");
        result.Should().NotContain("field `int ");
        result.Should().NotContain("field `string ");
    }

    // ── Primary constructor ───────────────────────────────────────────────
    // PrimaryCtorTestFixture line map:
    //   9-11: public sealed class PrimaryCtorTestFixture(IAnimalRepository, INotificationService) : IAnimalService

    [Test]
    public async Task AnalyzeAsync_PrimaryConstructorTypeDefinition_AppearsWithDefPrefix()
    {
        // arrange: lines 9-11
        // act
        var result = await ActPrimaryCtor(9, 11);

        // assert: short name for the class
        result.Should().Contain("def `PrimaryCtorTestFixture(");
    }

    [Test]
    public async Task AnalyzeAsync_PrimaryConstructorTypeDefinition_ShowsBaseTypes()
    {
        // arrange: lines 9-11
        // act
        var result = await ActPrimaryCtor(9, 11);

        // assert
        result.Should().Contain("TestProject.Core.Services.IAnimalService");
    }

    [Test]
    public async Task AnalyzeAsync_PrimaryConstructorTypeDefinition_ShowsAllParamTypes()
    {
        // arrange: lines 9-11
        // act
        var result = await ActPrimaryCtor(9, 11);

        // assert: C# style params
        result.Should().Contain("TestProject.Core.Persistence.IAnimalRepository animalRepository");
        result.Should().Contain("TestProject.Core.Services.INotificationService notificationService");
    }
}
