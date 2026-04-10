using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
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

    string Act(string typeQuery) =>
        GetTypeDepsAndUsageTool.FormatMarkdown(_depMap, typeQuery);

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
    public void FormatMarkdown_ImplWithUsages_ShowsCallsAndProps()
    {
        var md = Act("TestProject.Core.Services.IAnimalService");
        md.Should().MatchRegex(@"\[call\]|\[prop\]");
    }

    [Test]
    public void FormatMarkdown_AbstractionWithNoImpl_NoImplementationsSection()
    {
        var abstraction = new AbstractionInfo(
            FullName: "MyApp.IOrphan",
            Namespace: "MyApp",
            ProjectName: "MyApp",
            SourceFilePath: "/src/IOrphan.cs",
            IsInterface: true,
            DeclaredMembers: [],
            Implementations: []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo> { [abstraction.FullName] = abstraction },
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "MyApp.IOrphan");
        md.Should().NotContain("## Implementations");
        md.Should().NotContain("## Used by");
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
        var iDepA = new AbstractionInfo("MyApp.IDepA", "MyApp", "MyApp", "/src/IDepA.cs",
            true, [], ["MyApp.ImplA"]);
        var iDepB = new AbstractionInfo("MyApp.IDepB", "MyApp", "MyApp", "/src/IDepB.cs",
            true, [], ["MyApp.ImplA"]);
        var iConsumer = new AbstractionInfo("MyApp.IConsumer", "MyApp", "MyApp", "/src/IConsumer.cs",
            true, [], ["MyApp.ConsumerImpl"]);

        var consumerImpl = new ImplementationInfo(
            FullName: "MyApp.ConsumerImpl",
            Namespace: "MyApp",
            ProjectName: "MyApp",
            SourceFilePath: "/src/ConsumerImpl.cs",
            ImplementedAbstractions: ["MyApp.IConsumer"],
            BaseClasses: [],
            Dependencies: [
                new ConstructorDependency("MyApp.IDepA", false, false),
                new ConstructorDependency("MyApp.IDepB", false, false)
            ],
            DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>
            {
                ["MyApp.IDepA"] = [new MemberUsage("DoA", MemberUsageKind.MethodCall)],
                ["MyApp.IDepB"] = [new MemberUsage("DoB", MemberUsageKind.MethodCall)]
            });

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [iDepA.FullName] = iDepA,
                [iDepB.FullName] = iDepB,
                [iConsumer.FullName] = iConsumer
            },
            Implementations: new Dictionary<string, ImplementationInfo>
            {
                [consumerImpl.FullName] = consumerImpl
            });

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "MyApp.IDepA");

        md.Should().Contain("DoA()");
        md.Should().NotContain("DoB()");
    }

    [Test]
    public void FormatMarkdown_UsedBy_ImplWithNoUsagesOfQueried_NotShown()
    {
        var iDepA = new AbstractionInfo("MyApp.IDepA", "MyApp", "MyApp", "/src/IDepA.cs",
            true, [], ["MyApp.ImplA"]);
        var iConsumer = new AbstractionInfo("MyApp.IConsumer", "MyApp", "MyApp", "/src/IConsumer.cs",
            true, [], ["MyApp.ConsumerImpl"]);

        var consumerImpl = new ImplementationInfo(
            FullName: "MyApp.ConsumerImpl",
            Namespace: "MyApp",
            ProjectName: "MyApp",
            SourceFilePath: "/src/ConsumerImpl.cs",
            ImplementedAbstractions: ["MyApp.IConsumer"],
            BaseClasses: [],
            Dependencies: [new ConstructorDependency("MyApp.IDepA", false, false)],
            DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>());

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [iDepA.FullName] = iDepA,
                [iConsumer.FullName] = iConsumer
            },
            Implementations: new Dictionary<string, ImplementationInfo>
            {
                [consumerImpl.FullName] = consumerImpl
            });

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "MyApp.IDepA");
        md.Should().NotContain("## Used by");
    }

    #endregion

    #region NuGet abstraction

    [Test]
    public void FormatMarkdown_NuGetAbstraction_NoImplementationsSection()
    {
        var nuget = new AbstractionInfo(
            FullName: "Acme.IExternalService",
            Namespace: "Acme",
            ProjectName: "Acme",
            SourceFilePath: null,
            IsInterface: true,
            DeclaredMembers: [],
            Implementations: []);

        var consumer = new AbstractionInfo("MyApp.IConsumer", "MyApp", "MyApp", "/src/IConsumer.cs",
            true, [], ["MyApp.ConsumerImpl"]);

        var consumerImpl = new ImplementationInfo(
            FullName: "MyApp.ConsumerImpl",
            Namespace: "MyApp",
            ProjectName: "MyApp",
            SourceFilePath: "/src/ConsumerImpl.cs",
            ImplementedAbstractions: ["MyApp.IConsumer"],
            BaseClasses: [],
            Dependencies: [new ConstructorDependency("Acme.IExternalService", false, false)],
            DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>
            {
                ["Acme.IExternalService"] = [new MemberUsage("Execute", MemberUsageKind.MethodCall)]
            });

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [nuget.FullName] = nuget,
                [consumer.FullName] = consumer
            },
            Implementations: new Dictionary<string, ImplementationInfo>
            {
                [consumerImpl.FullName] = consumerImpl
            });

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "Acme.IExternalService");

        md.Should().NotContain("## Implementations");
        md.Should().Contain("## Used by");
        md.Should().Contain("Execute()");
    }

    #endregion

    #region Wildcard search

    [Test]
    public void FormatMarkdown_WildcardQuery_MatchesMultipleAbstractions()
    {
        var iA = new AbstractionInfo("MyApp.IServiceA", "MyApp", "MyApp", "/src/IServiceA.cs",
            true, [], []);
        var iB = new AbstractionInfo("MyApp.IServiceB", "MyApp", "MyApp", "/src/IServiceB.cs",
            true, [], []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [iA.FullName] = iA,
                [iB.FullName] = iB
            },
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "MyApp.IService*");
        md.Should().Contain("# MyApp.IServiceA");
        md.Should().Contain("# MyApp.IServiceB");
    }

    [Test]
    public void FormatMarkdown_WildcardQuery_NoMatches_ReturnsNotFound()
    {
        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>(),
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "*.INonExistent*");
        md.Should().Contain("No types found matching pattern");
        md.Should().Contain("*.INonExistent*");
    }

    #endregion

    #region Fallback fuzzy search

    [Test]
    public void FormatMarkdown_NoExactMatch_FallbackFindsAbstractions()
    {
        var iService = new AbstractionInfo("MyApp.Services.IAnimalService", "MyApp.Services", "MyApp",
            "/src/IAnimalService.cs", true, [], []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [iService.FullName] = iService
            },
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "IAnimalService");
        md.Should().Contain("No exact match found for `IAnimalService`");
        md.Should().Contain("# MyApp.Services.IAnimalService");
    }

    [Test]
    public void FormatMarkdown_NoExactMatch_FallbackFindsImplementations()
    {
        var impl = new ImplementationInfo(
            FullName: "MyApp.Services.AnimalService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/AnimalService.cs",
            ImplementedAbstractions: ["MyApp.Services.IAnimalService"],
            BaseClasses: [],
            Dependencies: [],
            DependencyMemberUsages: new Dictionary<string, IReadOnlyList<MemberUsage>>());

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>(),
            Implementations: new Dictionary<string, ImplementationInfo>
            {
                [impl.FullName] = impl
            });

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "AnimalService");
        md.Should().Contain("No exact match found for `AnimalService`");
        md.Should().Contain("## Matched implementations");
        md.Should().Contain("### MyApp.Services.AnimalService");
        md.Should().Contain("MyApp.Services.IAnimalService");
    }

    [Test]
    public void FormatMarkdown_NoExactMatch_FallbackNoResults()
    {
        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>(),
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "CompletelyUnknown");
        md.Should().Contain("No exact match found for `CompletelyUnknown`");
        md.Should().Contain("also returned no results");
    }

    [Test]
    public void FormatMarkdown_GenericFallback_NormalizesGenericParams()
    {
        var iRepo = new AbstractionInfo(
            "MyApp.IRepo<MyApp.Models.Animal>", "MyApp", "MyApp",
            "/src/IRepo.cs", true, [], []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [iRepo.FullName] = iRepo
            },
            Implementations: new Dictionary<string, ImplementationInfo>());

        // Query with specific generic param that doesn't match exactly
        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "IRepo<SomeOtherType>");
        md.Should().Contain("No exact match found");
        md.Should().Contain("*IRepo<*>*");
        md.Should().Contain("# MyApp.IRepo<MyApp.Models.Animal>");
    }

    [Test]
    public void FormatMarkdown_GenericFallback_MultipleTypeParams_PreservesArity()
    {
        var iMapper = new AbstractionInfo(
            "MyApp.IMapper<MyApp.Dto, MyApp.Entity>", "MyApp", "MyApp",
            "/src/IMapper.cs", true, [], []);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [iMapper.FullName] = iMapper
            },
            Implementations: new Dictionary<string, ImplementationInfo>());

        var md = GetTypeDepsAndUsageTool.FormatMarkdown(depMap, "IMapper<Foo, Bar>");
        md.Should().Contain("*IMapper<*, *>*");
        md.Should().Contain("# MyApp.IMapper<MyApp.Dto, MyApp.Entity>");
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

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
