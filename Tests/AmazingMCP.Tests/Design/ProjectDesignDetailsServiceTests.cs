using AmazingMCP.Configuration;
using AmazingMCP.Models;
using AmazingMCP.Models.Design;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class ProjectDesignDetailsServiceTests
{
    DependencyMapResult _depMap = null!;
    IDependencyMapService _dependencyMapService = null!;
    ProjectDesignDetailsService _sut = null!;
    CachedSolution _cachedSolution = null!;
    MemoryCache _cache = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.GetSharedSolutionAsync();

        _cache = new MemoryCache(new MemoryCacheOptions());
        var typeFilter = new TypeFilter();
        var depMapService = new DependencyMapService(
            CreateWorkspaceProvider(_cachedSolution),
            new TypeCollector(typeFilter),
            new MemberUsageAnalyzer(new InvocationAnalyzer(), new MemberAccessAnalyzer(), typeFilter),
            new AbstractionExtractor(),
            _cache);

        _depMap = await depMapService.BuildMapAsync(CompilationHelper.SolutionPath);

        _dependencyMapService = Substitute.For<IDependencyMapService>();
        _dependencyMapService
            .BuildMapAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_depMap);

        _sut = new ProjectDesignDetailsService(
            _dependencyMapService,
            new WildcardPatternFactory(),
            new DependencyAggregator(),
            Options.Create(new ProjectDesignOptions()));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _cache.Dispose();
    }

    string Act(string[] forNamespaces, bool includeDependencyUsage = true, bool includeImplementations = true) =>
        _sut.Format(_depMap, forNamespaces, includeDependencyUsage, includeImplementations);

    #region Namespace filtering — exact match

    [Test]
    public void Format_ExactMatch_ReturnsOnlyMatchingNamespace()
    {
        var md = Act(["TestProject.App.Mapping"]);

        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.Core.Services");
        md.Should().NotContain("TestProject.App.Messaging");
    }

    [Test]
    public void Format_ExactMatch_CaseInsensitive()
    {
        var md = Act(["testproject.app.mapping"]);
        md.Should().Contain("TestProject.App.Mapping");
    }

    [Test]
    public void Format_NoMatch_ReturnsErrorMessage()
    {
        var md = Act(["NonExistent.Namespace"]);
        md.Should().Contain("No abstractions found");
        md.Should().Contain("NonExistent.Namespace");
    }

    #endregion

    #region Namespace filtering — wildcard

    [Test]
    public void Format_WildcardSuffix_MatchesAllChildNamespaces()
    {
        var md = Act(["TestProject.App.*"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().Contain("TestProject.App.Messaging");
    }

    [Test]
    public void Format_WildcardSuffix_DoesNotMatchParent()
    {
        var md = Act(["TestProject.Core.*"]);
        md.Should().Contain("TestProject.Core.Services");
        md.Should().Contain("TestProject.Core.Persistence");
        md.Should().NotContain("TestProject.App.Mapping");
    }

    [Test]
    public void Format_WildcardPrefix_MatchesByNamespaceSuffix()
    {
        var md = Act(["*.Mapping"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.App.Mapping.Tv2");
    }

    [Test]
    public void Format_WildcardMiddle_MatchesCorrectly()
    {
        var md = Act(["TestProject.*.Mapping"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().NotContain("TestProject.App.Mapping.Tv2");
        md.Should().NotContain("TestProject.Core.Services");
    }

    [Test]
    public void Format_MultiplePatterns_UnionOfMatches()
    {
        var md = Act(["TestProject.App.Mapping", "TestProject.Core.Services"]);
        md.Should().Contain("TestProject.App.Mapping");
        md.Should().Contain("TestProject.Core.Services");
        md.Should().NotContain("TestProject.App.Messaging");
    }

    #endregion

    #region Output structure

    [Test]
    public void Format_ContainsHeader()
    {
        var md = Act(["TestProject.App.Mapping"]);
        md.Should().StartWith("# Project Design Details");
    }

    [Test]
    public void Format_AbstractionsAsH2Headers()
    {
        var md = Act(["TestProject.App.Mapping"]);
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        lines.Should().Contain(l => l.StartsWith("## ") && l.Contains("TestProject.App.Mapping"));
    }

    [Test]
    public void Format_ImplementationsListed()
    {
        var md = Act(["TestProject.App.Mapping"]);
        md.Should().Contain("### Implementations");
    }

    [Test]
    public void Format_DependenciesListed()
    {
        var md = Act(["TestProject.App.Messaging"]);
        md.Should().Contain("### Depends on");
    }

    [Test]
    public void Format_NuGetAbstractions_NotIncluded()
    {
        var md = Act(["AutoMapper"]);
        md.Should().Contain("No abstractions found");
    }

    #endregion

    #region includeDependencyUsage

    [Test]
    public void Format_IncludeDependencyUsage_True_ShowsUsages()
    {
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: true);
        md.Should().MatchRegex(@"\w+\(\)|\w+ \{get\}|\w+ \{set\}");
    }

    [Test]
    public void Format_IncludeDependencyUsage_False_HidesUsages()
    {
        var md = Act(["TestProject.App.Messaging"], includeDependencyUsage: false);
        md.Should().NotMatchRegex(@"  - \w+\(\)");
        md.Should().Contain("### Depends on");
    }

    #endregion

    #region includeImplementations

    [Test]
    public void Format_IncludeImplementations_True_ShowsImplementationsSection()
    {
        var md = Act(["TestProject.App.Mapping"], includeImplementations: true);
        md.Should().Contain("### Implementations");
    }

    [Test]
    public void Format_IncludeImplementations_False_HidesImplementationsSection()
    {
        var md = Act(["TestProject.App.Mapping"], includeImplementations: false);
        md.Should().NotContain("### Implementations");
        md.Should().Contain("### Depends on");
    }

    [Test]
    public void Format_IncludeImplementations_False_StillShowsDependencies()
    {
        var md = Act(["TestProject.App.Messaging"], includeImplementations: false, includeDependencyUsage: true);
        md.Should().Contain("### Depends on");
        md.Should().MatchRegex(@"\w+\(\)|\w+ \{get\}|\w+ \{set\}");
    }

    #endregion

    #region Truncation

    [Test]
    public void Format_LargeOutput_IsTruncated()
    {
        var md = Act(["TestProject.*"], includeDependencyUsage: true);
        if (md.Length >= 30000)
        {
            md.Should().Contain("<<... truncated output ...>>");
            md.Length.Should().BeLessThan(30000 + 300);
        }
    }

    [Test]
    public void Format_SmallOutput_NotTruncated()
    {
        var md = Act(["TestProject.Core.Configuration"]);
        md.Should().NotContain("<<... truncated output ...>>");
    }

    #endregion

    #region WildcardPattern unit tests

    [Test]
    public void WildcardPattern_ExactMatch_MatchesExact()
    {
        var pattern = new WildcardPatternFactory().CreateForTypeNames("MyApp.Services");
        pattern.IsMatch("MyApp.Services").Should().BeTrue();
        pattern.IsMatch("MyApp.Services.Extra").Should().BeFalse();
        pattern.IsMatch("Other.MyApp.Services").Should().BeFalse();
    }

    [Test]
    public void WildcardPattern_SuffixWildcard_MatchesChildren()
    {
        var pattern = new WildcardPatternFactory().CreateForTypeNames("MyApp.*");
        pattern.IsMatch("MyApp.Services").Should().BeTrue();
        pattern.IsMatch("MyApp.Services.Handlers").Should().BeTrue();
        pattern.IsMatch("MyApp").Should().BeFalse();
        pattern.IsMatch("Other.MyApp.Services").Should().BeFalse();
    }

    [Test]
    public void WildcardPattern_PrefixWildcard_MatchesBySuffix()
    {
        var pattern = new WildcardPatternFactory().CreateForTypeNames("*.Services");
        pattern.IsMatch("MyApp.Services").Should().BeTrue();
        pattern.IsMatch("Other.Services").Should().BeTrue();
        pattern.IsMatch("MyApp.Services.Extra").Should().BeFalse();
        pattern.IsMatch("Services").Should().BeFalse();
    }

    [Test]
    public void WildcardPattern_MiddleWildcard_MatchesCorrectly()
    {
        var pattern = new WildcardPatternFactory().CreateForTypeNames("MyApp.*.Services");
        pattern.IsMatch("MyApp.Core.Services").Should().BeTrue();
        pattern.IsMatch("MyApp.App.Services").Should().BeTrue();
        pattern.IsMatch("MyApp.Services").Should().BeFalse();
        pattern.IsMatch("MyApp.Core.Other").Should().BeFalse();
    }

    [Test]
    public void WildcardPattern_CaseInsensitive()
    {
        var pattern = new WildcardPatternFactory().CreateForTypeNames("myapp.services");
        pattern.IsMatch("MyApp.Services").Should().BeTrue();
        pattern.IsMatch("MYAPP.SERVICES").Should().BeTrue();
    }

    #endregion

    #region Format with synthetic data

    [Test]
    public void Format_EmptyNamespaces_ReturnsError()
    {
        var md = _sut.Format(_depMap, [], false);
        md.Should().Contain("No abstractions found");
    }

    [Test]
    public void Format_AbstractionWithNoImplementations_ShowsNoImplNote()
    {
        var md = Act(["TestProject.Core.Logging"]);
        md.Should().Contain("## TestProject.Core.Logging.IGenericTracer<TService>");
        var lines = md.Split('\n').ToList();
        var tracerIdx = lines.FindIndex(l => l.Contains("## TestProject.Core.Logging.IGenericTracer<TService>"));
        var nextH2Idx = lines.FindIndex(tracerIdx + 1, l => l.TrimStart().StartsWith("## "));
        var tracerSection = string.Join('\n', lines.Skip(tracerIdx).Take(
            nextH2Idx > tracerIdx ? nextH2Idx - tracerIdx : lines.Count - tracerIdx));
        tracerSection.Should().NotContain("### Implementations");
    }

    [Test]
    public void Format_DepLabel_ShowsTypeFullName()
    {
        var md = Act(["TestProject.App.Services"]);
        md.Should().Contain("TestProject.Core.Persistence.IAnimalRepository");
    }

    [Test]
    public void Format_Truncation_AppliedAtCorrectLength()
    {
        var md = Act(["TestProject.*"], includeDependencyUsage: true);
        if (md.Length >= 30000)
        {
            md.Should().Contain("<<... truncated output ...>>");
            md.Length.Should().BeLessThan(30000 + 300);
        }
    }

    #endregion

    #region XmlDoc summary

    [Test]
    public void Format_AbstractionWithXmlDocSummary_ShowsSummaryUnderHeader()
    {
        var md = Act(["TestProject.Core.Services"]);

        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        var headerIdx = lines.FindIndex(l => l == "## TestProject.Core.Services.IAnimalService");
        headerIdx.Should().BeGreaterThan(-1, "IAnimalService header must be present");

        var summaryLine = lines[headerIdx + 1];
        summaryLine.Should().StartWith("> ");
        summaryLine.Should().Contain("animal");
    }

    [Test]
    public void Format_AbstractionWithXmlDocSummary_SummaryAppearsInOutput()
    {
        var md = Act(["TestProject.Core.Persistence"]);
        md.Should().Contain("Repository for animal entities");
    }

    [Test]
    public void Format_AbstractionWithoutXmlDocSummary_NoSummaryLine()
    {
        var md = Act(["TestProject.Core.Persistence"]);

        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        var headerIdx = lines.FindIndex(l => l.StartsWith("## TestProject.Core.Persistence.IRepository<"));
        headerIdx.Should().BeGreaterThan(-1, "IRepository<T> header must be present");

        if (headerIdx + 1 < lines.Count)
            lines[headerIdx + 1].Should().NotStartWith("> ");
    }

    [Test]
    public void Format_TruncatedSummary_EndsWithTruncatedMarker()
    {
        var longText = new string('x', 2001);
        var abstraction = new AbstractionInfo
        {
            FullName = "My.Ns.IFoo",
            Namespace = "My.Ns",
            ProjectName = "MyProject",
            SourceFilePath = "/fake/IFoo.cs",
            IsInterface = true,
            IsAbstractClass = false,
            IsStaticClass = false,
            Implementations = [],
            XmlDocSummary = longText
        };

        var depMap = new DependencyMapResult(
            new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            new Dictionary<string, ImplementationInfo>(),
            null);

        var sut = new ProjectDesignDetailsService(
            Substitute.For<IDependencyMapService>(),
            new WildcardPatternFactory(),
            new DependencyAggregator(),
            Options.Create(new ProjectDesignOptions()));

        var md = sut.Format(depMap, ["My.Ns"], false, true);

        md.Should().Contain("<<truncated>>");
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        var headerIdx = lines.FindIndex(l => l == "## My.Ns.IFoo");
        headerIdx.Should().BeGreaterThan(-1, "abstraction header must be present");
        var summaryLine = lines[headerIdx + 1];
        summaryLine.Should().StartWith("> ");
        summaryLine.Should().EndWith("<<truncated>>");
    }

    [Test]
    public void ExtractXmlDocSummary_LongSummary_StoredInFull()
    {
        var longText = new string('x', 2001);
        var xml = $"<member><summary>{longText}</summary></member>";
        var result = AbstractionExtractor.ExtractXmlDocSummary(xml);

        result.Should().NotBeNull();
        result!.Length.Should().Be(2001);
        result.Should().NotContain("<<truncated>>");
    }

    [Test]
    public void ExtractXmlDocSummary_ShortSummary_NoTruncationMarker()
    {
        var xml = "<member><summary>Short summary.</summary></member>";
        var result = AbstractionExtractor.ExtractXmlDocSummary(xml);

        result.Should().Be("Short summary.");
        result.Should().NotContain("<<truncated>>");
    }

    #endregion

    #region get_project_design FormatMarkdown — intro block

    [Test]
    public void GetProjectDesignTool_FormatMarkdown_ContainsIntroBlock()
    {
        var result = new ProjectDesignProvider(_dependencyMapService, new DependencyAggregator())
            .BuildFromDependencyMap(_depMap, CompilationHelper.SolutionPath);
        var md = ProjectDesignService.Format(result);

        md.Should().Contain("get_project_design_details");
        md.Should().Contain("forNamespaces");
        md.Should().Contain("FullNamespace");
        md.Should().Contain("*");
    }

    #endregion
}
