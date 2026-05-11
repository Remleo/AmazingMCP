using AmazingMCP.Models;
using AmazingMCP.Services.Workspace;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class SolutionRecompilerTests
{
    CachedSolution _solution = null!;
    SolutionRecompiler _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _solution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new SolutionRecompiler(NullLogger<SolutionRecompiler>.Instance);
    }

    [Test]
    public async Task RecompileAsync_WhenFileNotInSolution_DoesNotThrow_AndReturnsUnchangedCompilations()
    {
        // arrange
        var compilationsBefore = _solution.Compilations;

        // act
        var act = () => _sut.RecompileAsync(_solution.Solution, compilationsBefore, [@"C:\nonexistent\ghost.cs"]);

        // assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task RecompileAsync_WhenDirtyFileInSolution_ReturnsNewCompilationInstance()
    {
        // arrange
        var anyDoc = _solution.Solution.Projects
            .SelectMany(p => p.Documents)
            .First(d => d.FilePath != null && File.Exists(d.FilePath));

        var filePath = anyDoc.FilePath!;
        var projectName = anyDoc.Project.Name;

        var compilationBefore = _solution.Compilations
            .First(c => c.ProjectName == projectName)
            .Compilation;

        // act
        var (_, updatedCompilations) = await _sut.RecompileAsync(
            _solution.Solution, _solution.Compilations, [filePath]);

        // assert
        var compilationAfter = updatedCompilations
            .First(c => c.ProjectName == projectName)
            .Compilation;

        compilationAfter.Should().NotBeSameAs(compilationBefore);
    }

    [Test]
    public async Task RecompileAsync_WhenNoDirtyFiles_ReturnsEquivalentCompilations()
    {
        // arrange
        var compilationsBefore = _solution.Compilations;

        // act
        var (_, updatedCompilations) = await _sut.RecompileAsync(_solution.Solution, compilationsBefore, []);

        // assert
        updatedCompilations.Should().BeEquivalentTo(compilationsBefore, opts => opts.WithStrictOrdering());
    }
}
