using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class CodeLensServiceTests
{
    // ── Variables ─────────────────────────────────────────────────────────
    // CodeLensTestFixture line map:
    //   29: var animal = _repository.FindById(id);
    //   30: return animal;
    //   36: var all = _repository.FindByKind(kind);
    //   37: var filtered = all.Where(a => a.Kind == kind).ToList();
    //   58: Animal? result = _repository.FindById(id);
    //   59: return result;

    [Test]
    public async Task AnalyzeAsync_LocalVariableRead_AppearsWithVarPrefix()
    {
        // arrange: line 30 — "return animal;" — read of local variable
        // act
        var result = await Act(30, 30);

        // assert
        result.Should().Contain("var `TestProject.Core.Models.Animal animal`");
    }

    [Test]
    public async Task AnalyzeAsync_LocalVariableDeclarationLine_NoVarEntry()
    {
        // arrange: line 29 — "var animal = _repository.FindById(id);"
        // declaration site — not a usage, should not produce a var entry
        // act
        var result = await Act(29, 29);

        // assert: no var entry — type is visible via call entry instead
        result.Should().NotContain("var `");
    }

    [Test]
    public async Task AnalyzeAsync_LocalVariableRead_DeduplicatedAcrossUsages()
    {
        // arrange: lines 29-31 — animal declared on 29, read on 30
        // act
        var result = await Act(29, 31);

        // assert: at most one var entry for animal
        CountOccurrences(result, "var `TestProject.Core.Models.Animal animal`").Should().BeLessThanOrEqualTo(1);
    }

    [Test]
    public async Task AnalyzeAsync_NullableLocalVariableRead_Appears()
    {
        // arrange: line 59 — "return result;" — Animal? result
        // act
        var result = await Act(59, 59);

        // assert
        result.Should().Contain("var `TestProject.Core.Models.Animal result`");
    }

    [Test]
    public async Task AnalyzeAsync_PrimitiveLocalVariable_NotIncluded()
    {
        // arrange: line 30 — only Animal variable in scope
        // act
        var result = await Act(30, 30);

        // assert: no primitive var entries
        result.Should().NotContain("var `int ");
        result.Should().NotContain("var `string ");
    }
}
