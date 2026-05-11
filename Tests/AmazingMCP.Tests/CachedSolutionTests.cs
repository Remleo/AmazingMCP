using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace AmazingMCP.Tests;

public class CachedSolutionTests
{
    CachedSolution _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _sut = await CompilationHelper.GetSharedSolutionAsync();
    }

    // UnsafeAccessor to read the private _dirtyFiles field
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dirtyFiles")]
    static extern ref System.Collections.Concurrent.ConcurrentDictionary<string, bool> GetDirtyFiles(CachedSolution target);

    [Test]
    public void MarkDirty_AddsFileToSet()
    {
        // arrange
        const string filePath = @"C:\some\file.cs";

        // act
        _sut.MarkDirty(filePath);

        // assert
        GetDirtyFiles(_sut).ContainsKey(filePath).Should().BeTrue();

        // cleanup
        GetDirtyFiles(_sut).TryRemove(filePath, out _);
    }

    [Test]
    public void MarkDirty_SameFileTwice_AppearsOnce()
    {
        // arrange
        const string filePath = @"C:\some\duplicate.cs";

        // act
        _sut.MarkDirty(filePath);
        _sut.MarkDirty(filePath);

        // assert
        GetDirtyFiles(_sut).Keys.Count(k => k == filePath).Should().Be(1);

        // cleanup
        GetDirtyFiles(_sut).TryRemove(filePath, out _);
    }

    [Test]
    public async Task EnsureUpToDateAsync_WhenNoDirtyFiles_DoesNotChangeCompilations()
    {
        // arrange
        GetDirtyFiles(_sut).Clear();
        var compilationsBefore = _sut.Compilations.Select(c => (c.ProjectName, c.Compilation)).ToList();

        // act
        await _sut.EnsureUpToDateAsync();

        // assert
        _sut.Compilations.Should().BeEquivalentTo(
            compilationsBefore,
            opts => opts.WithStrictOrdering());
    }

    [Test]
    public async Task EnsureUpToDateAsync_WhenDirtyFileNotInSolution_DoesNotThrow_AndClearsDirty()
    {
        // arrange
        const string unknownFile = @"C:\nonexistent\ghost.cs";
        _sut.MarkDirty(unknownFile);

        // act
        var act = () => _sut.EnsureUpToDateAsync();

        // assert
        await act.Should().NotThrowAsync();
        GetDirtyFiles(_sut).Should().BeEmpty();
    }

    [Test]
    public async Task EnsureUpToDateAsync_WhenDirtyFileInSolution_UpdatesCompilation()
    {
        // arrange
        var anyDoc = _sut.Solution.Projects
            .SelectMany(p => p.Documents)
            .First(d => d.FilePath != null && File.Exists(d.FilePath));

        var filePath = anyDoc.FilePath!;
        var projectName = anyDoc.Project.Name;

        var compilationBefore = _sut.Compilations
            .First(c => c.ProjectName == projectName)
            .Compilation;

        _sut.MarkDirty(filePath);

        // act
        await _sut.EnsureUpToDateAsync();

        // assert
        var compilationAfter = _sut.Compilations
            .First(c => c.ProjectName == projectName)
            .Compilation;

        // compilation object must be a new instance after recompile
        compilationAfter.Should().NotBeSameAs(compilationBefore);
        GetDirtyFiles(_sut).Should().BeEmpty();
    }

    [Test]
    public async Task EnsureUpToDateAsync_MultipleDirtyFilesInSameProject_ClearsAllDirty()
    {
        // arrange
        var docsInSameProject = _sut.Solution.Projects
            .SelectMany(p => p.Documents)
            .Where(d => d.FilePath != null && File.Exists(d.FilePath))
            .GroupBy(d => d.Project.Id)
            .First(g => g.Count() >= 2)
            .Take(2)
            .ToList();

        foreach (var doc in docsInSameProject)
            _sut.MarkDirty(doc.FilePath!);

        // act
        await _sut.EnsureUpToDateAsync();

        // assert
        GetDirtyFiles(_sut).Should().BeEmpty();
    }

    /*[Test]
    public async Task WorkspaceProvider_NestedTypeRemovedFromFile_IsNoLongerVisibleAfterFileChange()
    {
        // arrange — WatcherTestFixture.cs already contains TemporaryNested on disk at solution load time
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WorkspaceProvider(cache, NullLogger<WorkspaceProvider>.Instance);

        var solution = await provider.GetSolutionAsync(CompilationHelper.SolutionPath);

        var filePath = solution.Solution.Projects
            .SelectMany(p => p.Documents)
            .First(d => d.FilePath != null && d.FilePath.EndsWith("WatcherTestFixture.cs"))
            .FilePath!;

        var projectName = solution.Solution.Projects
            .First(p => p.Documents.Any(d => d.FilePath == filePath))
            .Name;

        bool HasTemporaryNested()
        {
            var compilation = solution.Compilations.First(c => c.ProjectName == projectName).Compilation;
            return compilation.GlobalNamespace
                .GetNamespaceMembers()
                .SelectMany(RoslynTypeEnumerator.EnumerateAll)
                .Any(t => t.Name == "TemporaryNested");
        }

        // assert — nested type is visible in the initial load
        HasTemporaryNested().Should().BeTrue("nested type should be visible in the initial compilation");

        // act — remove nested type from file
        await File.WriteAllTextAsync(filePath, """
            namespace TestProject.Core.Models;

            public class WatcherTestFixture
            {
            }
            """);

        await Task.Delay(500);
        solution = await provider.GetSolutionAsync(CompilationHelper.SolutionPath);

        // assert — nested type is gone
        HasTemporaryNested().Should().BeFalse("nested type should not be visible after removing it");

        // cleanup — restore original file
        await File.WriteAllTextAsync(filePath, """
            namespace TestProject.Core.Models;

            public class WatcherTestFixture
            {
                public class TemporaryNested { }
            }
            """);
    }*/
}
