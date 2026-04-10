using AmazingMCP.Models;
using AmazingMCP.Services;
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
        var depMapService = new DependencyMapService(
            new TestWorkspaceProvider(_cachedSolution),
            new TypeCollector(),
            new ConstructorAnalyzer(),
            new MemberUsageAnalyzer(),
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
        GetDetailedProjectDesignTool.FormatMarkdown(_depMap, forNamespaces, includeDependencyUsage, includeImplementations);

    #region Namespace filtering — exact match

    [Test]
    public void FormatMarkdown_ExactMatch_ReturnsOnlyMatchingNamespace()
    {
        // act
        var md = Act(["TestProject.App.Mapping"]);

        // assert
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.Core.Services");
        md.Should().NotContain("TestProject.App.Messaging");
    }

    [Test]
    public void FormatMarkdown_ExactMatch_CaseInsensitive()
    {
        // act
        var md = Act(["testproject.app.mapping"]);

        // assert
        md.Should().Contain("TestProject.App.Mapping");
    }

    [Test]
    public void FormatMarkdown_NoMatch_ReturnsErrorMessage()
    {
        // act
        var md = Act(["NonExistent.Namespace"]);

        // assert
        md.Should().Contain("No abstractions found");
        md.Should().Contain("NonExistent.Namespace");
    }

    #endregion

    #region Namespace filtering — wildcard

    [Test]
    public void FormatMarkdown_WildcardSuffix_MatchesAllChildNamespaces()
    {
        // act
        var md = Act(["TestProject.App.*"]);

        // assert — should match App.Mapping, App.Messaging, App.Services, etc.
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().Contain("TestProject.App.Messaging");
    }

    [Test]
    public void FormatMarkdown_WildcardSuffix_DoesNotMatchParent()
    {
        // act — TestProject.App.* should NOT match TestProject.App itself (if it existed)
        var md = Act(["TestProject.Core.*"]);

        // assert — matches children
        md.Should().Contain("TestProject.Core.Services");
        md.Should().Contain("TestProject.Core.Persistence");
        // but not something outside Core
        md.Should().NotContain("TestProject.App.Mapping");
    }

    [Test]
    public void FormatMarkdown_WildcardPrefix_MatchesByNamespaceSuffix()
    {
        // act
        var md = Act(["*.Mapping"]);

        // assert
        md.Should().Contain("TestProject.App.Mapping");
        // sub-namespaces like Mapping.Tv2 should NOT match *.Mapping (exact segment)
        md.Should().NotContain("TestProject.App.Mapping.Tv2");
    }

    [Test]
    public void FormatMarkdown_WildcardMiddle_MatchesCorrectly()
    {
        // act
        var md = Act(["TestProject.*.Mapping"]);

        // assert
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.App.Mapping.Tv2");
        md.Should().NotContain("TestProject.Core.Services");
    }

    [Test]
    public void FormatMarkdown_MultiplePatterns_UnionOfMatches()
    {
        // act
        var md = Act(["TestProject.App.Mapping", "TestProject.Core.Services"]);

        // assert
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().Contain("TestProject.Core.Services");
        md.Should().NotContain("TestProject.App.Messaging");
    }

    #endregion

    #region Output structure

    [Test]
    public void FormatMarkdown_ContainsHeader()
    {
        // act
        var md = Act(["TestProject.App.Mapping"]);

        // assert
        md.Should().StartWith("# Detailed Project Design");
    }

    [Test]
    public void FormatMarkdown_AbstractionsAsH2Headers()
    {
        // act
        var md = Act(["TestProject.App.Mapping"]);

        // assert — abstractions shown as ## headers
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        lines.Should().Contain(l => l.StartsWith("## ") && l.Contains("TestProject.App.Mapping"));
    }

    [Test]
    public void FormatMarkdown_ImplementationsListed()
    {
        // act
        var md = Act(["TestProject.App.Mapping"]);

        // assert
        md.Should().Contain("### Implementations");
    }

    [Test]
    public void FormatMarkdown_DependenciesListed()
    {
        // act
        var md = Act(["TestProject.App.Messaging"]);

        // assert
        md.Should().Contain("### Depends on");
    }

    [Test]
    public void FormatMarkdown_NuGetAbstractions_NotIncluded()
    {
        // act — NuGet types have SourceFilePath = null, should be excluded
        var md = Act(["AutoMapper"]);

        // assert
        md.Should().Contain("No abstractions found");
    }

    #endregion

    #region includeDependencyUsage

    [Test]
    public void FormatMarkdown_IncludeDependencyUsage_True_ShowsUsages()
    {
        // act
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: true);

        // assert
        md.Should().MatchRegex(@"\[call\]|\[prop\]");
    }

    [Test]
    public void FormatMarkdown_IncludeDependencyUsage_False_HidesUsages()
    {
        // act
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: false);

        // assert
        md.Should().NotContain("[call]");
        md.Should().NotContain("[prop]");
        md.Should().Contain("### Depends on");
    }

    #endregion

    #region includeImplementations

    [Test]
    public void FormatMarkdown_IncludeImplementations_True_ShowsImplementationsSection()
    {
        // act
        var md = Act(["TestProject.App.Mapping"], includeImplementations: true);

        // assert
        md.Should().Contain("### Implementations");
    }

    [Test]
    public void FormatMarkdown_IncludeImplementations_False_HidesImplementationsSection()
    {
        // act
        var md = Act(["TestProject.App.Mapping"], includeImplementations: false);

        // assert
        md.Should().NotContain("### Implementations");
        md.Should().Contain("### Depends on");
    }

    [Test]
    public void FormatMarkdown_IncludeImplementations_False_StillShowsDependencies()
    {
        // act
        var md = Act(["TestProject.App.Messaging"], includeImplementations: false, includeDependencyUsage: true);

        // assert
        md.Should().Contain("### Depends on");
        md.Should().MatchRegex(@"\[call\]|\[prop\]");
    }

    #endregion

    #region Truncation

    [Test]
    public void FormatMarkdown_LargeOutput_IsTruncated()
    {
        // act — use wildcard to get a large result
        var md = Act(["TestProject.*"], includeDependencyUsage: true);

        // assert
        if (md.Length >= 10000)
        {
            md.Should().Contain("<<... truncated output ...>>");
            md.Length.Should().BeLessThan(10000 + 300);
        }
    }

    [Test]
    public void FormatMarkdown_SmallOutput_NotTruncated()
    {
        // act — single specific namespace should be small
        var md = Act(["TestProject.Core.Configuration"]);

        // assert
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
        // empty array — no patterns, no matches
        md.Should().Contain("No abstractions found");
    }

    [Test]
    public void FormatMarkdown_AbstractionWithNoImplementations_ShowsNoImplNote()
    {
        // arrange — synthetic depMap with abstraction that has no implementations
        var abstraction = new AbstractionInfo(
            FullName: "MyApp.Services.IOrphanService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/IOrphanService.cs",
            IsInterface: true,
            DeclaredMembers: [],
            Implementations: []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            Implementations: new Dictionary<string, ImplementationInfo>());

        // act
        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        // assert — no implementations section, no error text
        md.Should().NotContain("### Implementations");
        md.Should().Contain("## MyApp.Services.IOrphanService");
    }

    [Test]
    public void FormatMarkdown_IOptionsDepLabel_FormattedCorrectly()
    {
        // arrange
        var abstraction = new AbstractionInfo(
            FullName: "MyApp.Services.IMyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/IMyService.cs",
            IsInterface: true,
            DeclaredMembers: [],
            Implementations: ["MyApp.Services.MyService"]);

        var impl = new ImplementationInfo(
            FullName: "MyApp.Services.MyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/MyService.cs",
            ImplementedAbstractions: ["MyApp.Services.IMyService"],
            BaseClasses: [],
            Dependencies: [new ConstructorDependency("MyApp.Config.AppSettings", IsOptions: true, IsEnumerable: false)],
            DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>());

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            Implementations: new Dictionary<string, ImplementationInfo> { [impl.FullName] = impl });

        // act
        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        // assert
        md.Should().Contain("IOptions<MyApp.Config.AppSettings>");
    }

    [Test]
    public void FormatMarkdown_IEnumerableDepLabel_FormattedCorrectly()
    {
        // arrange
        var abstraction = new AbstractionInfo(
            FullName: "MyApp.Services.IMyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/IMyService.cs",
            IsInterface: true,
            DeclaredMembers: [],
            Implementations: ["MyApp.Services.MyService"]);

        var impl = new ImplementationInfo(
            FullName: "MyApp.Services.MyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/MyService.cs",
            ImplementedAbstractions: ["MyApp.Services.IMyService"],
            BaseClasses: [],
            Dependencies: [new ConstructorDependency("MyApp.Handlers.IHandler", IsOptions: false, IsEnumerable: true)],
            DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>());

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            Implementations: new Dictionary<string, ImplementationInfo> { [impl.FullName] = impl });

        // act
        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        // assert
        md.Should().Contain("IEnumerable<MyApp.Handlers.IHandler>");
    }

    [Test]
    public void FormatMarkdown_Truncation_AppliedAtCorrectLength()
    {
        // arrange — generate a depMap large enough to trigger truncation
        var abstractions = new Dictionary<string, AbstractionInfo>();
        var implementations = new Dictionary<string, ImplementationInfo>();

        for (var i = 0; i < 100; i++)
        {
            var ns = "MyApp.Services";
            var ifaceName = $"MyApp.Services.IService{i:D3}";
            var implName = $"MyApp.Services.Service{i:D3}";

            abstractions[ifaceName] = new AbstractionInfo(
                FullName: ifaceName, Namespace: ns, ProjectName: "MyApp",
                SourceFilePath: $"/src/IService{i}.cs", IsInterface: true,
                DeclaredMembers: [], Implementations: [implName]);

            implementations[implName] = new ImplementationInfo(
                FullName: implName, Namespace: ns, ProjectName: "MyApp",
                SourceFilePath: $"/src/Service{i}.cs",
                ImplementedAbstractions: [ifaceName], BaseClasses: [],
                Dependencies: [new ConstructorDependency($"MyApp.Deps.IDep{i}", false, false)],
                DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>
                {
                    [$"MyApp.Deps.IDep{i}"] = [new MemberUsage($"DoSomething{i}", MemberUsageKind.MethodCall)]
                });
        }

        var depMap = new DependencyMapResult(abstractions, implementations);

        // act
        var md = GetDetailedProjectDesignTool.FormatMarkdown(depMap, ["MyApp.Services"], true);

        // assert
        md.Should().Contain("<<... truncated output ...>>");
        md.Length.Should().BeLessThanOrEqualTo(10000 + 300);
    }

    #endregion

    #region get_project_design FormatMarkdown — intro block

    [Test]
    public void GetProjectDesignTool_FormatMarkdown_ContainsIntroBlock()
    {
        // arrange
        var result = ProjectDesignService.BuildFromDependencyMap(_depMap, CompilationHelper.SolutionPath);

        // act
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert
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
