using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class RoslynSymbolServiceTests
{
    CachedSolution _cachedSolution = null!;
    RoslynSymbolService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.LoadTestSolutionAsync();
        _sut = new RoslynSymbolService(new TestWorkspaceProvider(_cachedSolution), new WildcardPatternFactory());
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _cachedSolution.Dispose();

    async Task<(string? FullName, string? Error)> Act(string fullTypeName)
    {
        var (symbol, error) = await _sut.FindExactTypeAsync(CompilationHelper.SolutionPath, fullTypeName);
        return (symbol?.ToDisplayString(), error);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_UnknownType_ReturnsNotFound()
    {
        // act
        var (fullName, error) = await Act("NonExistent.Type");

        // assert
        fullName.Should().BeNull();
        error.Should().Contain("not found");
    }

    // ── Non-generic ───────────────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_NonGenericType_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.Models.AnimalKind");
    }

    // ── Generic — C# syntax ───────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_GenericType_CSharpSyntax_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.Logging.IGenericTracer<TService>");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.Logging.IGenericTracer<TService>");
    }

    // ── Generic — CLR backtick notation ──────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_GenericType_ClrBacktickNotation_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.Logging.IGenericTracer`1");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.Logging.IGenericTracer<TService>");
    }

    // ── Generic — two type params ─────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_TwoParamGeneric_CSharpSyntax_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.EventHandling.IEventHandler<TEvent, TResult>");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.EventHandling.IEventHandler<TEvent, TResult>");
    }

    [Test]
    public async Task FindExactTypeAsync_TwoParamGeneric_ClrBacktickNotation_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.EventHandling.IEventHandler`2");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.EventHandling.IEventHandler<TEvent, TResult>");
    }

    // ── Wrong arity ───────────────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_WrongArity_ReturnsNotFound()
    {
        // act — IGenericTracer has arity 1, requesting arity 2
        var (fullName, error) = await Act("TestProject.Core.Logging.IGenericTracer`2");

        // assert
        fullName.Should().BeNull();
        error.Should().Contain("not found");
    }

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
