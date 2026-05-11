using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using AmazingMCP.Tools;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class GetTypeDepsAndUsageToolTests
{
    DependencyMapResult _depMap = null!;
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
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _cache.Dispose();
    }

    string Act(string typeQuery) =>
        GetTypeDepsAndUsageTool.FormatMarkdown(_depMap, typeQuery, new WildcardPatternFactory(), new DependencyAggregator());

    #region Exact match

    [Test]
    public void FormatMarkdown_ExactMatch_HeaderIsFullName()
    {
        var md = Act("TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
        md.Should().StartWith("# TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    [Test]
    public void FormatMarkdown_ExactMatch_ShowsImplementationsSection()
    {
        var md = Act("TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
        md.Should().Contain("## Implementations");
        md.Should().Contain("TestProject.App.Persistence.AnimalRepository");
    }

    [Test]
    public void FormatMarkdown_ImplWithDeps_ShowsDependsOn()
    {
        var md = Act("TestProject.Core.Services.IAnimalService");
        md.Should().Contain("Depends on:");
    }

    [Test]
    public void FormatMarkdown_AbstractionWithNoImpl_NoImplementationsSection()
    {
        // IGenericTracer<TService> open generic has no source implementations
        var md = Act("TestProject.Core.Logging.IGenericTracer<TService>");
        md.Should().NotContain("## Implementations");
    }

    #endregion

    #region Used by section

    [Test]
    public void FormatMarkdown_UsedBy_ShowsConsumers()
    {
        var md = Act("TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
        md.Should().Contain("## Used by");
    }

    [Test]
    public void FormatMarkdown_UsedBy_GroupedByAbstraction()
    {
        var md = Act("TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        var usedByIdx = lines.IndexOf("## Used by");
        usedByIdx.Should().BeGreaterThan(0);
        lines.Skip(usedByIdx).Should().Contain(l => l.StartsWith("### "));
    }

    [Test]
    public void FormatMarkdown_UsedBy_OnlyShowsUsagesOfQueriedAbstraction()
    {
        // IAnimalRepository is used by multiple classes — each shows only its own usages
        var md = Act("TestProject.Core.Persistence.IAnimalRepository");
        // FindById is called by AnimalService
        md.Should().Contain("FindById()");
        // Save is called by AnimalService — both should appear
        md.Should().Contain("Save()");
    }

    [Test]
    public void FormatMarkdown_UsedBy_ImplWithNoUsagesOfQueried_NotShown()
    {
        // IUnusedLogger has an implementation but nobody calls Log() on it
        var md = Act("TestProject.Core.Logging.IUnusedLogger");
        md.Should().NotContain("## Used by");
    }

    #endregion

    #region NuGet abstraction

    [Test]
    public void FormatMarkdown_NuGetAbstraction_NoImplementationsSection()
    {
        // AutoMapper.IMapper is a NuGet type — no source implementations
        var md = Act("AutoMapper.IMapperBase");
        md.Should().NotContain("## Implementations");
        md.Should().Contain("## Used by");
    }

    #endregion

    #region Wildcard search

    [Test]
    public void FormatMarkdown_WildcardQuery_MatchesMultipleAbstractions()
    {
        // *IEntityMapper* matches IEntityMapper, IEntityMapperV2, IEntityMapperV3, IEntityMapperV4
        var md = Act("*IEntityMapper*");
        md.Should().Contain("# TestProject.App.Mapping.IEntityMapper");
        md.Should().Contain("# TestProject.App.Mapping.Tv2.IEntityMapperV2");
    }

    [Test]
    public void FormatMarkdown_WildcardQuery_NoMatches_ReturnsNotFound()
    {
        var md = Act("*.INonExistentXyz*");
        md.Should().Contain("No types found matching pattern");
        md.Should().Contain("*.INonExistentXyz*");
    }

    #endregion

    #region Fallback fuzzy search

    [Test]
    public void FormatMarkdown_NoExactMatch_FallbackFindsAbstractions()
    {
        var md = Act("IAnimalService");
        md.Should().Contain("No exact match found for `IAnimalService`");
        md.Should().Contain("# TestProject.Core.Services.IAnimalService");
    }

    [Test]
    public void FormatMarkdown_NoExactMatch_FallbackFindsImplementations()
    {
        var md = Act("AnimalService");
        md.Should().Contain("No exact match found for `AnimalService`");
        md.Should().Contain("## Matched implementations");
        md.Should().Contain("### TestProject.App.Services.AnimalService");
    }

    [Test]
    public void FormatMarkdown_NoExactMatch_FallbackNoResults()
    {
        var md = Act("CompletelyUnknownXyzAbc");
        md.Should().Contain("No exact match found for `CompletelyUnknownXyzAbc`");
        md.Should().Contain("also returned no results");
    }

    [Test]
    public void FormatMarkdown_GenericFallback_NormalizesGenericParams()
    {
        // Searching for IRepository<SomeOtherType> should fuzzy-match IRepository<Animal>
        var md = Act("IRepository<SomeOtherType>");
        md.Should().Contain("No exact match found");
        md.Should().Contain("*IRepository<*>*");
        md.Should().Contain("# TestProject.Core.Persistence.IRepository<TestProject.Core.Models.Animal>");
    }

    [Test]
    public void FormatMarkdown_GenericFallback_MultipleTypeParams_PreservesArity()
    {
        // IEntityMapper<TSource, TDest> — searching with wrong params should still fuzzy-match
        var md = Act("IEntityMapper<Foo, Bar>");
        md.Should().Contain("*IEntityMapper<*, *>*");
    }

    #endregion

    #region Duplicate implementation deduplication

    [Test]
    public void FormatMarkdown_SameImplUnderMultipleAbstractions_ShownInFullOnlyOnce()
    {
        // MultiRoleService implements IMultiRoleServiceA and IMultiRoleServiceB
        // Both interfaces match *MultiRole* — MultiRoleService appears under each,
        // but full deps should only be printed the first time
        var md = Act("*MultiRole*");

        var firstIdx = md.IndexOf("### TestProject.App.Services.MultiRoleService",
            StringComparison.Ordinal);
        firstIdx.Should().BeGreaterThan(0, "MultiRoleService should appear at least once");

        var secondIdx = md.IndexOf("### TestProject.App.Services.MultiRoleService",
            firstIdx + 1, StringComparison.Ordinal);
        secondIdx.Should().BeGreaterThan(firstIdx, "MultiRoleService should appear under both interfaces");

        md[secondIdx..].Should().Contain("*(see first occurrence above)*");
    }

    [Test]
    public void FormatMarkdown_SameImplUnderMultipleAbstractions_DepsNotRepeated()
    {
        var md = Act("*MultiRole*");

        // IAnimalRepository should appear exactly once in Depends on sections
        var count = 0;
        var idx = 0;
        while ((idx = md.IndexOf("- TestProject.Core.Persistence.IAnimalRepository", idx,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }
        count.Should().Be(1, "dependencies should only be listed once");
    }

    #endregion

    #region NormalizeForFuzzySearch

    [Test]
    public void NormalizeForFuzzySearch_SimpleName_WrapsWithWildcards()
    {
        var result = GetTypeDepsAndUsageTool.NormalizeForFuzzySearch("IMyService");
        result.Should().Be("*IMyService*");
    }

    [Test]
    public void NormalizeForFuzzySearch_GenericSingleParam_ReplacesWithEmpty()
    {
        var result = GetTypeDepsAndUsageTool.NormalizeForFuzzySearch("IFoo<int>");
        result.Should().Be("*IFoo<*>*");
    }

    [Test]
    public void NormalizeForFuzzySearch_GenericMultipleParams_PreservesCommas()
    {
        var result = GetTypeDepsAndUsageTool.NormalizeForFuzzySearch("IFoo<int, string, Bwin.Sports.Bar>");
        result.Should().Be("*IFoo<*, *, *>*");
    }

    [Test]
    public void NormalizeForFuzzySearch_AlreadyHasWildcards_DoesNotDoubleWrap()
    {
        var result = GetTypeDepsAndUsageTool.NormalizeForFuzzySearch("*IFoo*");
        result.Should().Be("*IFoo*");
    }

    #endregion
}

