using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    DependencyMapService _sut = null!;
    IWorkspaceProvider _workspaceProvider = null!;
    CachedSolution _cachedSolution = null!;
    MemoryCache _cache = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.LoadTestSolutionAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _cachedSolution.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        _workspaceProvider = Substitute.For<IWorkspaceProvider>();
        _workspaceProvider.GetSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(_cachedSolution);

        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new DependencyMapService(
            _workspaceProvider,
            new TypeCollector(),
            new ConstructorAnalyzer(),
            new MemberUsageAnalyzer(),
            new AbstractionExtractor(),
            _cache);
    }

    [TearDown]
    public void TearDown()
    {
        _cache.Dispose();
    }

    async Task<DependencyMapResult> Act() =>
        await _sut.BuildMapAsync(CompilationHelper.SolutionPath);
}
