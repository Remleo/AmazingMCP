using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Tests.Helpers;
using AmazingMCP.Tools;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace AmazingMCP.Tests;

/// <summary>
/// Tests for closed→open generic collapsing in GetTypeDepsAndUsageTool and GetProjectDesignDetailsTool.
///
/// Test data: IGenericTracer&lt;TService&gt; (open generic, source-defined in TestProject.Core.Logging)
///   - IGenericTracer&lt;TracedServiceA&gt; — used by TracedServiceA via tracer?.Trace(...)
///   - IGenericTracer&lt;TracedServiceB&gt; — used by TracedServiceB via tracer?.Trace(...)
/// Both closed variants have no source-defined implementations → collapse into open generic.
/// </summary>
public class GenericCollapseTests
{
    DependencyMapResult _depMap = null!;
    CachedSolution _cachedSolution = null!;
    MemoryCache _cache = null!;

    const string OpenTracer = "TestProject.Core.Logging.IGenericTracer<TService>";
    const string ClosedTracerA = "TestProject.Core.Logging.IGenericTracer<TestProject.App.Services.TracedServiceA>";
    const string ClosedTracerB = "TestProject.Core.Logging.IGenericTracer<TestProject.App.Services.TracedServiceB>";
    const string TracedServiceA = "TestProject.App.Services.TracedServiceA";
    const string TracedServiceB = "TestProject.App.Services.TracedServiceB";

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.LoadTestSolutionAsync();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var typeFilter = new TypeFilter();
        var depMapService = new DependencyMapService(
            new TestWorkspaceProvider(_cachedSolution),
            new TypeCollector(typeFilter),
            new MemberUsageAnalyzer(new InvocationAnalyzer(), new MemberAccessAnalyzer(), typeFilter),
            new AbstractionExtractor(),
            _cache);
        _depMap = await depMapService.BuildMapAsync(CompilationHelper.SolutionPath);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _cache.Dispose();
        _cachedSolution.Dispose();
    }

    string Act(string typeQuery) =>
        GetTypeDepsAndUsageTool.FormatMarkdown(_depMap, typeQuery, new DependencyAggregator());

    string ActDetailed(string[] forNamespaces) =>
        GetProjectDesignDetailsTool.FormatMarkdown(
            _depMap, forNamespaces, includeDependencyUsage: true,
            includeImplementations: true, aggregator: new DependencyAggregator());

    // ─── Preconditions ───────────────────────────────────────────────────────

    [Test]
    public void Precondition_ClosedToOpenMap_ContainsBothClosedVariants()
    {
        _depMap.ClosedToOpenGenericMap.Should().ContainKey(ClosedTracerA)
            .WhoseValue.Should().Be(OpenTracer);
        _depMap.ClosedToOpenGenericMap.Should().ContainKey(ClosedTracerB)
            .WhoseValue.Should().Be(OpenTracer);
    }

    [Test]
    public void Precondition_OpenGenericAbstraction_ExistsInAbstractions()
    {
        _depMap.Abstractions.Should().ContainKey(OpenTracer);
    }

    [Test]
    public void Precondition_ClosedVariants_HaveNoSourceImplementations()
    {
        _depMap.Abstractions[ClosedTracerA].Implementations.Should().BeEmpty();
        _depMap.Abstractions[ClosedTracerB].Implementations.Should().BeEmpty();
    }

    // ─── GetTypeDepsAndUsageTool — open generic in match ────────────────────

    [Test]
    public void FormatMarkdown_OpenGenericMatched_ClosedVariantsNotShownSeparately()
    {
        var md = Act("*IGenericTracer*");

        md.Should().Contain($"# {OpenTracer}");
        md.Should().NotContain($"# {ClosedTracerA}");
        md.Should().NotContain($"# {ClosedTracerB}");
    }

    [Test]
    public void FormatMarkdown_OpenGenericMatched_AggregatesUsedByFromAllCloseds()
    {
        var md = Act("*IGenericTracer*");

        // Both consumers must appear under the open generic's Used by
        md.Should().Contain(TracedServiceA);
        md.Should().Contain(TracedServiceB);
    }

    [Test]
    public void FormatMarkdown_OpenGenericMatched_AggregatesUsages()
    {
        var md = Act("*IGenericTracer*");

        // Trace() called in TracedServiceA — must appear aggregated
        md.Should().Contain("Trace()");
    }

    [Test]
    public void FormatMarkdown_OpenGenericMatched_CollapseAllCloseds_EvenNonMatched()
    {
        // Query matches only the open generic — not the closed variants
        var md = Act($"*IGenericTracer<TService>*");

        md.Should().Contain($"# {OpenTracer}");
        md.Should().NotContain($"# {ClosedTracerA}");
        md.Should().NotContain($"# {ClosedTracerB}");
        // Both consumers still appear (aggregated from non-matched closeds)
        md.Should().Contain(TracedServiceA);
        md.Should().Contain(TracedServiceB);
    }

    // ─── GetTypeDepsAndUsageTool — open generic NOT in match ────────────────

    [Test]
    public void FormatMarkdown_OpenGenericNotMatched_ClosedVariantsShownSeparately()
    {
        // Query matches only closed variants (by concrete type name), not the open generic
        var md = Act($"*IGenericTracer<TestProject.App*");

        md.Should().Contain($"# {ClosedTracerA}");
        md.Should().Contain($"# {ClosedTracerB}");
        md.Should().NotContain($"# {OpenTracer}");
    }

    [Test]
    public void FormatMarkdown_OpenGenericNotMatched_EachClosedShowsItsOwnUsedBy()
    {
        var md = Act($"*IGenericTracer<TestProject.App*");

        // Each closed shows its own consumer
        md.Should().Contain(TracedServiceA);
        md.Should().Contain(TracedServiceB);
    }

    // ─── GetProjectDesignDetailsTool — Depends on collapsing ───────────────

    [Test]
    public void DetailedProjectDesign_DependsOn_ClosedGenericCollapsedToOpen()
    {
        // ITracedService is in TestProject.App.Services, implemented by TracedServiceA/B
        // Both depend on IGenericTracer<TracedServiceA/B> (closed) → should show as IGenericTracer<TService>
        var md = ActDetailed(["TestProject.App.Services"]);

        md.Should().Contain($"- {OpenTracer}");
        md.Should().NotContain($"- {ClosedTracerA}");
        md.Should().NotContain($"- {ClosedTracerB}");
    }

    [Test]
    public void DetailedProjectDesign_DependsOn_ClosedGenericUsagesAggregated()
    {
        var md = ActDetailed(["TestProject.App.Services"]);

        // Trace() is called in TracedServiceA and TracedServiceB — must appear under open generic dep
        md.Should().Contain("Trace()");
    }

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
