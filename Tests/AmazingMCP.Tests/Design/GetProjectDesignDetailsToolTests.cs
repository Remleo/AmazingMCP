using AmazingMCP.Models;
using AmazingMCP.Models.Design;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using AmazingMCP.Tools;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class GetProjectDesignDetailsToolTests
{
    DependencyMapResult _depMap = null!;
    IDependencyMapService _dependencyMapService = null!;
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
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _cache.Dispose();
    }

    string Act(string[] forNamespaces, bool includeDependencyUsage = true, bool includeImplementations = true) =>
        GetProjectDesignDetailsTool.FormatMarkdown(
            _depMap, forNamespaces, includeDependencyUsage, includeImplementations, new WildcardPatternFactory(), new DependencyAggregator());

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
        md.Should().StartWith("# Project Design Details");
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

    #region FormatMarkdown with synthetic data

    [Test]
    public void FormatMarkdown_EmptyNamespaces_ReturnsError()
    {
        var md = GetProjectDesignDetailsTool.FormatMarkdown(_depMap, [], true);
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

    #region XmlDoc summary

    [Test]
    public void FormatMarkdown_AbstractionWithXmlDocSummary_ShowsSummaryUnderHeader()
    {
        var md = Act(["TestProject.Core.Services"]);

        // IAnimalService has a summary — it should appear right after the ## header
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        var headerIdx = lines.FindIndex(l => l == "## TestProject.Core.Services.IAnimalService");
        headerIdx.Should().BeGreaterThan(-1, "IAnimalService header must be present");

        var summaryLine = lines[headerIdx + 1];
        summaryLine.Should().StartWith("> ");
        summaryLine.Should().Contain("animal");
    }

    [Test]
    public void FormatMarkdown_AbstractionWithXmlDocSummary_SummaryAppearsInOutput()
    {
        var md = Act(["TestProject.Core.Persistence"]);

        // IAnimalRepository has a summary
        md.Should().Contain("Repository for animal entities");
    }

    [Test]
    public void FormatMarkdown_AbstractionWithoutXmlDocSummary_NoSummaryLine()
    {
        var md = Act(["TestProject.Core.Persistence"]);

        // IRepository<T> has no summary — no blockquote line should follow its header
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        var headerIdx = lines.FindIndex(l => l.StartsWith("## TestProject.Core.Persistence.IRepository<"));
        headerIdx.Should().BeGreaterThan(-1, "IRepository<T> header must be present");

        // The line immediately after the header should NOT be a blockquote summary
        if (headerIdx + 1 < lines.Count)
            lines[headerIdx + 1].Should().NotStartWith("> ");
    }

    [Test]
    public void FormatMarkdown_TruncatedSummary_EndsWithTruncatedMarker()
    {
        // ExtractXmlDocSummary stores the full text; FormatMarkdown truncates at 2000 chars
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

        var md = GetProjectDesignDetailsTool.FormatMarkdown(depMap, ["My.Ns"], false, true, null);

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
        // The extractor must NOT truncate — full text is preserved in the data structure
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
        var result = new ProjectDesignService(_dependencyMapService, new DependencyAggregator())
            .BuildFromDependencyMap(_depMap, CompilationHelper.SolutionPath);
        var md = GetProjectDesignTool.FormatMarkdown(result);

        md.Should().Contain("get_project_design_details");
        md.Should().Contain("forNamespaces");
        md.Should().Contain("FullNamespace");
        md.Should().Contain("*");
    }

    #endregion
}
