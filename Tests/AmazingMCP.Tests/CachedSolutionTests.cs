using AmazingMCP.Models;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
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
    public void DrainDirtyFiles_ReturnsAllDirtyAndClearsSet()
    {
        // arrange
        _sut.MarkDirty(@"C:\a.cs");
        _sut.MarkDirty(@"C:\b.cs");

        // act
        var drained = _sut.DrainDirtyFiles();

        // assert
        drained.Should().BeEquivalentTo([@"C:\a.cs", @"C:\b.cs"]);
        _sut.HasDirtyFiles.Should().BeFalse();
    }
}
