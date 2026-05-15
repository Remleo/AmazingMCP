using AmazingMCP.Services;
using AmazingMCP.Services.SymbolQuery;
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
    public async Task NestedTypeAddedThenRemovedFromFile_IsVisibleThenGone()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var recompiler = new SolutionRecompiler(NullLogger<SolutionRecompiler>.Instance);
        var solutionCache = new SolutionCache(memoryCache);
        var solutionWatcher = new SolutionWatcher(NullLogger<SolutionWatcher>.Instance);
        var solutionLoader = new SolutionLoader();
        using var provider = new WorkspaceProvider(solutionLoader, solutionCache, solutionWatcher, recompiler, NullLogger<WorkspaceProvider>.Instance);

        // arrange — load solution while WatcherTestFixture.cs has no nested type
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
                .SelectMany(RoslynTypeEnumerator.EnumerateAllInCompilation)
                .Any(t => t.Name == "TemporaryNested");
        }

        HasTemporaryNested().Should().BeFalse("nested type should not exist in the initial load");

        // act — add nested type
        await File.WriteAllTextAsync(filePath, """
            namespace TestProject.Core.Models;

            public class WatcherTestFixture
            {
                public class TemporaryNested { }
            }
            """);

        await Task.Delay(500);
        solution = await provider.GetSolutionAsync(CompilationHelper.SolutionPath);

        HasTemporaryNested().Should().BeTrue("nested type should be visible after adding it");

        // act — remove nested type (restore stable state)
        await File.WriteAllTextAsync(filePath, """
            namespace TestProject.Core.Models;

            public class WatcherTestFixture
            {
            }
            """);

        await Task.Delay(500);
        solution = await provider.GetSolutionAsync(CompilationHelper.SolutionPath);

        HasTemporaryNested().Should().BeFalse("nested type should not be visible after removing it");
    }
}
