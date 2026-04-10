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
        md.Should().MatchRegex(@"\[call\]|\[prop\]");
    }

    [Test]
    public void FormatMarkdown_IncludeDependencyUsage_False_HidesUsages()
    {
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: false);
        md.Should().NotContain("[call]");
        md.Should().NotContain("[prop]");
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
        md.Should().MatchRegex(@"\[call\]|\[prop\]");
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
        var abstraction = new AbstractionInfo(
            FullName: "MyApp.Services.IOrphanService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/IOrphanService.cs",
            IsInterface: true,
            IsAbstractClass: false,
            IsStaticClass: false,
            Implementations: []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        md.Should().NotContain("### Implementations");
        md.Should().Contain("## MyApp.Services.IOrphanService");
    }

    [Test]
    public void FormatMarkdown_DepLabel_ShowsTypeFullName()
    {
        var abstraction = new AbstractionInfo(
            FullName: "MyApp.Services.IMyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/IMyService.cs",
            IsInterface: true,
            IsAbstractClass: false,
            IsStaticClass: false,
            Implementations: ["MyApp.Services.MyService"]);

        var impl = new ImplementationInfo(
            FullName: "MyApp.Services.MyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/MyService.cs",
            ImplementedAbstractions: ["MyApp.Services.IMyService"],
            BaseClasses: [],
            Dependencies: [new AbstractionUsage("MyApp.Config.AppSettings", false, [])]);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            Implementations: new Dictionary<string, ImplementationInfo> { [impl.FullName] = impl });

        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        md.Should().Contain("MyApp.Config.AppSettings");
    }

    [Test]
    public void FormatMarkdown_Truncation_AppliedAtCorrectLength()
    {
        var abstractions = new Dictionary<string, AbstractionInfo>();
        var implementations = new Dictionary<string, ImplementationInfo>();

        for (var i = 0; i < 300; i++)
        {
            var ns = "MyApp.Services";
            var ifaceName = $"MyApp.Services.IService{i:D3}";
            var implName = $"MyApp.Services.Service{i:D3}";

            abstractions[ifaceName] = new AbstractionInfo(
                FullName: ifaceName, Namespace: ns, ProjectName: "MyApp",
                SourceFilePath: $"/src/IService{i}.cs", IsInterface: true,
                IsAbstractClass: false, IsStaticClass: false,
                Implementations: [implName]);

            implementations[implName] = new ImplementationInfo(
                FullName: implName, Namespace: ns, ProjectName: "MyApp",
                SourceFilePath: $"/src/Service{i}.cs",
                ImplementedAbstractions: [ifaceName], BaseClasses: [],
                Dependencies: [new AbstractionUsage($"MyApp.Deps.IDep{i}", false,
                    [new MemberUsage($"DoSomething{i}", MemberUsageKind.MethodCall)])]);
        }

        var depMap = new DependencyMapResult(abstractions, implementations);
        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        md.Should().Contain("<<... truncated output ...>>");
        md.Length.Should().BeLessThanOrEqualTo(30000 + 300);
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
