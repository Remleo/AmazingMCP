using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Tests.Helpers;
using AmazingMCP.Tools;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class GetDetailedProjectDesignToolTests
{
    DependencyMapResult _depMap = null!;
    CachedSolution _cachedSolution = null!;
    MemoryCache _cache = null!;

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

    string Act(string[] forNamespaces, bool includeDependencyUsage = true, bool includeImplementations = true) =>
        GetDetailedProjectDesignTool.FormatMarkdown(
            _depMap, forNamespaces, includeDependencyUsage, includeImplementations, new DependencyAggregator());

    #region Namespace filtering — exact match

    [Test]
    public void FormatMarkdown_ExactMatch_ReturnsOnlyMatchingNamespace()
    {
        var md = Act(["TestProject.App.Mapping"]);

        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.Core.Services");
        md.Should().NotContain("TestProject.App.Messaging");
    }

    [Test]
    public void FormatMarkdown_ExactMatch_CaseInsensitive()
    {
        var md = Act(["testproject.app.mapping"]);
        md.Should().Contain("TestProject.App.Mapping");
    }

    [Test]
    public void FormatMarkdown_NoMatch_ReturnsErrorMessage()
    {
        var md = Act(["NonExistent.Namespace"]);
        md.Should().Contain("No abstractions found");
        md.Should().Contain("NonExistent.Namespace");
    }

    #endregion

    #region Namespace filtering — wildcard

    [Test]
    public void FormatMarkdown_WildcardSuffix_MatchesAllChildNamespaces()
    {
        var md = Act(["TestProject.App.*"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().Contain("TestProject.App.Messaging");
    }

    [Test]
    public void FormatMarkdown_WildcardSuffix_DoesNotMatchParent()
    {
        var md = Act(["TestProject.Core.*"]);
        md.Should().Contain("TestProject.Core.Services");
        md.Should().Contain("TestProject.Core.Persistence");
        md.Should().NotContain("TestProject.App.Mapping");
    }

    [Test]
    public void FormatMarkdown_WildcardPrefix_MatchesByNamespaceSuffix()
    {
        var md = Act(["*.Mapping"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.App.Mapping.Tv2");
    }

    [Test]
    public void FormatMarkdown_WildcardMiddle_MatchesCorrectly()
    {
        var md = Act(["TestProject.*.Mapping"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.App.Mapping.Tv2");
        md.Should().NotContain("TestProject.Core.Services");
    }

    [Test]
    public void FormatMarkdown_MultiplePatterns_UnionOfMatches()
    {
        var md = Act(["TestProject.App.Mapping", "TestProject.Core.Services"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().Contain("TestProject.Core.Services");
        md.Should().NotContain("TestProject.App.Messaging");
    }

    #endregion

    #region Output structure

    [Test]
    public void FormatMarkdown_ContainsHeader()
    {
        var md = Act(["TestProject.App.Mapping"]);
        md.Should().StartWith("# Detailed Project Design");
    }

    [Test]
    public void FormatMarkdown_AbstractionsAsH2Headers()
    {
        var md = Act(["TestProject.App.Mapping"]);
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        lines.Should().Contain(l => l.StartsWith("## ") && l.Contains("TestProject.App.Mapping"));
    }

    [Test]
    public void FormatMarkdown_ImplementationsListed()
    {
        var md = Act(["TestProject.App.Mapping"]);
        md.Should().Contain("### Implementations");
    }

    [Test]
    public void FormatMarkdown_DependenciesListed()
    {
        var md = Act(["TestProject.App.Messaging"]);
        md.Should().Contain("### Depends on");
    }

    [Test]
    public void FormatMarkdown_NuGetAbstractions_NotIncluded()
    {
        var md = Act(["AutoMapper"]);
        md.Should().Contain("No abstractions found");
    }

    #endregion

    #region includeDependencyUsage

    [Test]
    public void FormatMarkdown_IncludeDependencyUsage_True_ShowsUsages()
    {
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: true);
        md.Should().MatchRegex(@"\w+\(\)|\w+ \{get\}|\w+ \{set\}");
    }

    [Test]
    public void FormatMarkdown_IncludeDependencyUsage_False_HidesUsages()
    {
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: false);
        md.Should().NotMatchRegex(@"  - \w+\(\)");
        md.Should().Contain("### Depends on");
    }

    #endregion

    #region includeImplementations

    [Test]
    public void FormatMarkdown_IncludeImplementations_True_ShowsImplementationsSection()
    {
        var md = Act(["TestProject.App.Mapping"], includeImplementations: true);
        md.Should().Contain("### Implementations");
    }

    [Test]
    public void FormatMarkdown_IncludeImplementations_False_HidesImplementationsSection()
    {
        var md = Act(["TestProject.App.Mapping"], includeImplementations: false);
        md.Should().NotContain("### Implementations");
        md.Should().Contain("### Depends on");
    }

    [Test]
    public void FormatMarkdown_IncludeImplementations_False_StillShowsDependencies()
    {
        var md = Act(["TestProject.App.Messaging"], includeImplementations: false, includeDependencyUsage: true);
        md.Should().Contain("### Depends on");
        md.Should().MatchRegex(@"\w+\(\)|\w+ \{get\}|\w+ \{set\}");
    }

    #endregion

    #region Truncation

    [Test]
    public void FormatMarkdown_LargeOutput_IsTruncated()
    {
        var md = Act(["TestProject.*"], includeDependencyUsage: true);
        if (md.Length >= 30000)
        {
            md.Should().Contain("<<... truncated output ...>>");
            md.Length.Should().BeLessThan(30000 + 300);
        }
    }

    [Test]
    public void FormatMarkdown_SmallOutput_NotTruncated()
    {
        var md = Act(["TestProject.Core.Configuration"]);
        md.Should().NotContain("<<... truncated output ...>>");
    }

    #endregion

    #region WildcardToRegex unit tests

    [Test]
    public void WildcardToRegex_ExactMatch_MatchesExact()
    {
        var regex = GetDetailedProjectDesignTool.WildcardToRegex("MyApp.Services");
        regex.IsMatch("MyApp.Services").Should().BeTrue();
        regex.IsMatch("MyApp.Services.Extra").Should().BeFalse();
        regex.IsMatch("Other.MyApp.Services").Should().BeFalse();
    }

    [Test]
    public void WildcardToRegex_SuffixWildcard_MatchesChildren()
    {
        var regex = GetDetailedProjectDesignTool.WildcardToRegex("MyApp.*");
        regex.IsMatch("MyApp.Services").Should().BeTrue();
        regex.IsMatch("MyApp.Services.Handlers").Should().BeTrue();
        regex.IsMatch("MyApp").Should().BeFalse();
        regex.IsMatch("Other.MyApp.Services").Should().BeFalse();
    }

    [Test]
    public void WildcardToRegex_PrefixWildcard_MatchesBySuffix()
    {
        var regex = GetDetailedProjectDesignTool.WildcardToRegex("*.Services");
        regex.IsMatch("MyApp.Services").Should().BeTrue();
        regex.IsMatch("Other.Services").Should().BeTrue();
        regex.IsMatch("MyApp.Services.Extra").Should().BeFalse();
        regex.IsMatch("Services").Should().BeFalse();
    }

    [Test]
    public void WildcardToRegex_MiddleWildcard_MatchesCorrectly()
    {
        var regex = GetDetailedProjectDesignTool.WildcardToRegex("MyApp.*.Services");
        regex.IsMatch("MyApp.Core.Services").Should().BeTrue();
        regex.IsMatch("MyApp.App.Services").Should().BeTrue();
        regex.IsMatch("MyApp.Services").Should().BeFalse();
        regex.IsMatch("MyApp.Core.Other").Should().BeFalse();
    }

    [Test]
    public void WildcardToRegex_CaseInsensitive()
    {
        var regex = GetDetailedProjectDesignTool.WildcardToRegex("myapp.services");
        regex.IsMatch("MyApp.Services").Should().BeTrue();
        regex.IsMatch("MYAPP.SERVICES").Should().BeTrue();
    }

    #endregion

    #region FormatMarkdown with synthetic data

    [Test]
    public void FormatMarkdown_EmptyNamespaces_ReturnsError()
    {
        var md = GetDetailedProjectDesignTool.FormatMarkdown(_depMap, [], true);
        md.Should().Contain("No abstractions found");
    }

    [Test]
    public void FormatMarkdown_AbstractionWithNoImplementations_ShowsNoImplNote()
    {
        // IGenericTracer<TService> is an open generic with no source implementations
        var md = Act(["TestProject.Core.Logging"]);
        md.Should().Contain("## TestProject.Core.Logging.IGenericTracer<TService>");
        // The IGenericTracer<TService> entry specifically has no implementations
        var lines = md.Split('\n').ToList();
        var tracerIdx = lines.FindIndex(l => l.Contains("## TestProject.Core.Logging.IGenericTracer<TService>"));
        var nextH2Idx = lines.FindIndex(tracerIdx + 1, l => l.TrimStart().StartsWith("## "));
        var tracerSection = string.Join('\n', lines.Skip(tracerIdx).Take(
            nextH2Idx > tracerIdx ? nextH2Idx - tracerIdx : lines.Count - tracerIdx));
        tracerSection.Should().NotContain("### Implementations");
    }

    [Test]
    public void FormatMarkdown_DepLabel_ShowsTypeFullName()
    {
        // AnimalService depends on IAnimalRepository — full name must appear in Depends on
        var md = Act(["TestProject.App.Services"]);
        md.Should().Contain("TestProject.Core.Persistence.IAnimalRepository");
    }

    [Test]
    public void FormatMarkdown_Truncation_AppliedAtCorrectLength()
    {
        var md = Act(["TestProject.*"], includeDependencyUsage: true);
        if (md.Length >= 30000)
        {
            md.Should().Contain("<<... truncated output ...>>");
            md.Length.Should().BeLessThan(30000 + 300);
        }
    }

    #endregion

    #region get_project_design FormatMarkdown — intro block

    [Test]
    public void GetProjectDesignTool_FormatMarkdown_ContainsIntroBlock()
    {
        var result = ProjectDesignService.BuildFromDependencyMap(
            _depMap, CompilationHelper.SolutionPath, new DependencyAggregator());
        var md = GetProjectDesignTool.FormatMarkdown(result);

        md.Should().Contain("get_detailed_project_design");
        md.Should().Contain("forNamespaces");
        md.Should().Contain("FullNamespace");
        md.Should().Contain("*");
    }

    #endregion

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
