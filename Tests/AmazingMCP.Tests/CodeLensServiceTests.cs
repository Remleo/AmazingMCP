using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

/// <summary>
/// Integration tests for <see cref="CodeLensService"/> against the real TestSolution.
/// Each test targets a specific line range in CodeLensTestFixture.cs and asserts
/// that the output contains the expected type-resolved entries.
/// </summary>
[TestFixture]
public class CodeLensServiceTests
{
    CodeLensService _sut = null!;
    IWorkspaceProvider _workspaceProvider = null!;

    // Path to the fixture file inside TestSolution
    static readonly string FixturePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "TestData", "TestSolution",
        "TestProject.App", "Helpers", "CodeLensTestFixture.cs"));

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();

        _workspaceProvider = Substitute.For<IWorkspaceProvider>();
        _workspaceProvider
            .GetSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(cachedSolution);

        _sut = new CodeLensService(_workspaceProvider);
    }

    async Task<string> Act(int startLine, int endLine)
        => await _sut.AnalyzeAsync(CompilationHelper.SolutionPath, FixturePath, startLine, endLine);

    // ── Variables ─────────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_LocalVariableOfDomainType_AppearsInVariablesSection()
    {
        // arrange: line 28 — "var animal = _repository.FindById(id);"
        // act
        var result = await Act(28, 28);

        // assert
        result.Should().Contain("## Variables");
        result.Should().Contain("var animal: TestProject.Core.Models.Animal");
    }

    [Test]
    public async Task AnalyzeAsync_NullableLocalVariable_AppearsWithNullableType()
    {
        // arrange: line 57 — "Animal? result = _repository.FindById(id);"
        // act
        var result = await Act(57, 57);

        // assert
        result.Should().Contain("## Variables");
        result.Should().Contain("var result: TestProject.Core.Models.Animal");
    }

    [Test]
    public async Task AnalyzeAsync_PrimitiveLocalVariable_NotIncluded()
    {
        // arrange: line 28 only — "var animal = ..." — id is int (primitive), should not appear
        // act
        var result = await Act(28, 28);

        // assert: no primitive types
        result.Should().NotContain("var id:");
        result.Should().NotContain(": int");
        result.Should().NotContain(": Int32");
    }

    // ── Calls ─────────────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_MethodCallReturningDomainType_AppearsInCallsSection()
    {
        // arrange: lines 27-30 — GetById body with _repository.FindById(id)
        // act
        var result = await Act(27, 30);

        // assert
        result.Should().Contain("## Calls");
        result.Should().Contain(".FindById(");
        result.Should().Contain("→ TestProject.Core.Models.Animal");
    }

    [Test]
    public async Task AnalyzeAsync_MethodCallWithDomainArg_ShowsArgType()
    {
        // arrange: lines 41-52 — Add body with _repository.Save(animal)
        // act
        var result = await Act(41, 52);

        // assert: Save takes Animal
        result.Should().Contain(".Save(");
        result.Should().Contain("[0] TestProject.Core.Models.Animal");
    }

    [Test]
    public async Task AnalyzeAsync_MethodCallsDeduplicatedByName()
    {
        // arrange: lines 41-52 — Add body calls Notify twice
        // act
        var result = await Act(41, 52);

        // assert: Notify appears only once
        var notifyCount = CountOccurrences(result, ".Notify(");
        notifyCount.Should().Be(1);
    }

    [Test]
    public async Task AnalyzeAsync_MethodCallReturningPrimitive_ReturnTypeOmitted()
    {
        // arrange: lines 41-52 — Notify returns void (trivial)
        // act
        var result = await Act(41, 52);

        // assert: .Notify line has no → arrow (void return omitted)
        var notifyLine = result.Split('\n').FirstOrDefault(l => l.Contains(".Notify("));
        notifyLine.Should().NotBeNull();
        notifyLine!.Should().NotContain("→");
    }

    // ── Extensions ────────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_LinqExtensionCall_AppearsInExtensionsSection()
    {
        // arrange: lines 35-37 — GetByKind body with .Where(...).ToList()
        // act
        var result = await Act(35, 37);

        // assert
        result.Should().Contain("## Extensions");
        result.Should().Contain(".Where(");
    }

    [Test]
    public async Task AnalyzeAsync_ExtensionCall_ShowsReceiverType()
    {
        // arrange: lines 35-37 — .Where called on IReadOnlyList<Animal>
        // act
        var result = await Act(35, 37);

        // assert: receiver type shown after "on"
        result.Should().Contain(" on ");
    }

    [Test]
    public async Task AnalyzeAsync_ExtensionCallsDeduplicatedByName()
    {
        // arrange: lines 35-37 — Where and ToList each appear once
        // act
        var result = await Act(35, 37);

        // assert
        var whereCount = CountOccurrences(result, ".Where(");
        whereCount.Should().Be(1);
    }

    // ── Definitions ───────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_TypeDefinition_AppearsInDefinitionsSection()
    {
        // arrange: line 15 — "public class CodeLensTestFixture : IAnimalService"
        // act
        var result = await Act(15, 15);

        // assert
        result.Should().Contain("## Definitions");
        result.Should().Contain("def TestProject.App.Helpers.CodeLensTestFixture");
    }

    [Test]
    public async Task AnalyzeAsync_TypeDefinition_ShowsBaseTypesFromSyntax()
    {
        // arrange: line 15 — CodeLensTestFixture : IAnimalService
        // act
        var result = await Act(15, 15);

        // assert: IAnimalService is listed as base type
        result.Should().Contain("TestProject.Core.Services.IAnimalService");
    }

    [Test]
    public async Task AnalyzeAsync_ConstructorDefinition_AppearsInDefinitionsSection()
    {
        // arrange: lines 19-22 — constructor of CodeLensTestFixture
        // act
        var result = await Act(19, 22);

        // assert
        result.Should().Contain("## Definitions");
        result.Should().Contain("def new CodeLensTestFixture(");
    }

    [Test]
    public async Task AnalyzeAsync_ConstructorDefinition_ShowsNonTrivialParamTypes()
    {
        // arrange: lines 19-22 — ctor params: IAnimalRepository, INotificationService
        // act
        var result = await Act(19, 22);

        // assert
        result.Should().Contain("[0] TestProject.Core.Persistence.IAnimalRepository");
        result.Should().Contain("[1] TestProject.Core.Services.INotificationService");
    }

    [Test]
    public async Task AnalyzeAsync_MethodDefinition_AppearsInDefinitionsSection()
    {
        // arrange: line 27 — "public Animal? GetById(int id)"
        // act
        var result = await Act(27, 27);

        // assert
        result.Should().Contain("## Definitions");
        result.Should().Contain("def GetById(");
    }

    [Test]
    public async Task AnalyzeAsync_MethodDefinition_ReturnTypeShown_WhenNonTrivial()
    {
        // arrange: line 27 — GetById returns Animal?
        // act
        var result = await Act(27, 27);

        // assert: return type is Animal (non-trivial)
        result.Should().Contain("→ TestProject.Core.Models.Animal");
    }

    // ── System namespace trimming ─────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_SystemTypes_NamespaceTrimmed()
    {
        // arrange: lines 35-37 — LINQ returns IEnumerable<Animal>, IReadOnlyList<Animal>
        // act
        var result = await Act(35, 37);

        // assert: no "System.Collections.Generic." prefix in output
        result.Should().NotContain("System.Collections.Generic.IEnumerable");
        result.Should().NotContain("System.Collections.Generic.IReadOnlyList");
        // but short names should appear (at least one of these)
        var hasShortName = result.Contains("IEnumerable<") || result.Contains("IReadOnlyList<") || result.Contains("List<");
        hasShortName.Should().BeTrue("System.* namespace should be trimmed to short name");
    }

    // ── No results ────────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_FileNotFound_ReturnsFileNotFoundMessage()
    {
        // arrange: non-existent file path
        var missingFile = Path.Combine(Path.GetTempPath(), "Missing_" + Guid.NewGuid() + ".cs");

        // act
        var result = await _sut.AnalyzeAsync(
            CompilationHelper.SolutionPath, missingFile, 1, 5);

        // assert
        result.Should().StartWith("File not found in solution:");
    }

    // ── File not found ────────────────────────────────────────────────────

    [Test]
    public async Task AnalyzeAsync_FileNotInSolution_ReturnsErrorMessage()
    {
        // arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent.cs");

        // act
        var result = await _sut.AnalyzeAsync(
            CompilationHelper.SolutionPath, nonExistentPath, 1, 10);

        // assert
        result.Should().StartWith("File not found in solution:");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
