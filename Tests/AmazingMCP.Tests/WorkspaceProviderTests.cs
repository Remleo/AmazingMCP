using AmazingMCP.Services;
using AmazingMCP.Services.Workspace;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class WorkspaceProviderTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Ensure MSBuild is registered before WorkspaceProvider creates its own MSBuildWorkspace
        await CompilationHelper.GetSharedSolutionAsync();
    }

    [Test]
    public async Task NestedTypeRemovedFromFile_IsNoLongerVisibleAfterFileChange()
    {
        // arrange — WatcherTestFixture.cs already contains TemporaryNested on disk at solution load time
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var recompiler = new SolutionRecompiler(NullLogger<SolutionRecompiler>.Instance);
        var provider = new WorkspaceProvider(cache, NullLogger<WorkspaceProvider>.Instance, recompiler);

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
    }
}
