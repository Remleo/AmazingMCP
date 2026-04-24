using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Tests.Helpers;
using AmazingMCP.Tools;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class ProjectDesignServiceTests
{
    DependencyMapResult _depMap = null!;
    IDependencyAggregator _aggregator = null!;
    IDependencyMapService _dependencyMapService = null!;
    CachedSolution _cachedSolution = null!;
    MemoryCache _cache = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _aggregator = new DependencyAggregator();

        _cache = new MemoryCache(new MemoryCacheOptions());
        var typeFilter = new TypeFilter();
        var depMapService = new DependencyMapService(
            new TestWorkspaceProvider(_cachedSolution),
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

    ProjectDesignResult Act() =>
        new ProjectDesignService(_dependencyMapService, _aggregator).BuildFromDependencyMap(_depMap, CompilationHelper.SolutionPath);

    #region Flat groups — no project split

    [Test]
    public void BuildFromDependencyMap_ContainsCoreGroups()
    {
        // act
        var result = Act();

        // assert
        var fullNames = result.Groups.Select(g => g.FullName).ToList();
        fullNames.Should().Contain("TestProject.Core.Persistence");
        fullNames.Should().Contain("TestProject.Core.Services");
        fullNames.Should().Contain("TestProject.Core.Logging");
        fullNames.Should().Contain("TestProject.Core.EventHandling");
        fullNames.Should().Contain("TestProject.Core.Notifications");
    }

    [Test]
    public void BuildFromDependencyMap_ContainsAppGroups()
    {
        // act
        var result = Act();

        // assert
        var fullNames = result.Groups.Select(g => g.FullName).ToList();
        fullNames.Should().Contain("TestProject.App.Services");
        fullNames.Should().Contain("TestProject.App.Services.GenericConsumers");
        fullNames.Should().Contain("TestProject.App.Messaging");
        fullNames.Should().Contain("TestProject.App.Mapping");
    }

    [Test]
    public void BuildFromDependencyMap_NoInfrastructureGroups()
    {
        // act
        var result = Act();

        // assert — Infrastructure types are implementations of Core/App abstractions,
        // they don't form their own abstraction groups
        result.Groups.Where(g => g.FullName.StartsWith("TestProject.Infrastructure"))
            .Should().BeEmpty();
    }

    [Test]
    public void BuildFromDependencyMap_HasMappingSubVersionGroups()
    {
        // act
        var result = Act();

        // assert
        var fullNames = result.Groups.Select(g => g.FullName).ToList();
        fullNames.Should().Contain("TestProject.App.Mapping.Tv2");
        fullNames.Should().Contain("TestProject.App.Mapping.Tv3");
        fullNames.Should().Contain("TestProject.App.Mapping.Tv4");
    }

    [Test]
    public void BuildFromDependencyMap_ServicesGroup_HasEntries()
    {
        // act
        var result = Act();

        // assert
        var servicesGroup = result.Groups.Single(g => g.FullName == "TestProject.Core.Services");
        servicesGroup.EntryCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Test]
    public void BuildFromDependencyMap_PersistenceGroup_HasEntries()
    {
        // act
        var result = Act();

        // assert
        var persistenceGroup = result.Groups.Single(g => g.FullName == "TestProject.Core.Persistence");
        persistenceGroup.EntryCount.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Group dependencies — DependsOn (now full namespaces)

    [Test]
    public void BuildFromDependencyMap_MessagingGroup_DependsOnEventHandlingAndMapping()
    {
        // act
        var result = Act();

        // assert — MessageHandlers depend on IEntityMapper (Mapping) and IEventDispatcher (EventHandling)
        var messagingGroup = result.Groups.Single(g => g.FullName == "TestProject.App.Messaging");
        messagingGroup.DependsOn.Should().Contain("TestProject.Core.EventHandling");
        messagingGroup.DependsOn.Should().Contain("TestProject.App.Mapping");
    }

    [Test]
    public void BuildFromDependencyMap_ServicesGroup_DependsOnPersistence()
    {
        // act
        var result = Act();

        // assert
        var servicesGroup = result.Groups.Single(g => g.FullName == "TestProject.Core.Services");
        servicesGroup.DependsOn.Should().Contain("TestProject.Core.Persistence");
    }

    [Test]
    public void BuildFromDependencyMap_GenericConsumersGroup_DependsOnPersistenceAndEventHandling()
    {
        // act
        var result = Act();

        // assert — GenericConsumerService depends on IRepository<Animal> (Persistence) and IEventHandler (EventHandling)
        var group = result.Groups.Single(g => g.FullName == "TestProject.App.Services.GenericConsumers");
        group.DependsOn.Should().Contain("TestProject.Core.Persistence");
        group.DependsOn.Should().Contain("TestProject.Core.EventHandling");
    }

    [Test]
    public void BuildFromDependencyMap_InternalDepsWithinGroup_NotInDependsOn()
    {
        // act
        var result = Act();

        // assert
        foreach (var group in result.Groups)
        {
            group.DependsOn.Should().NotContain(group.FullName);
        }
    }

    [Test]
    public void BuildFromDependencyMap_ExternalDepsAreAlwaysKnownGroups()
    {
        // act
        var result = Act();

        // assert — every DependsOn entry either resolves to a real source group,
        // or is a NuGet namespace (no group, but valid dependency target)
        var allGroupFullNames = result.Groups.Select(g => g.FullName).ToHashSet();
        var allAbstractionNamespaces = _depMap.Abstractions.Values
            .Select(a => a.Namespace)
            .ToHashSet();

        foreach (var group in result.Groups)
        {
            foreach (var dep in group.DependsOn)
            {
                // dep must be either a known group OR a known abstraction namespace (incl. NuGet)
                var isKnown = allGroupFullNames.Contains(dep) || allAbstractionNamespaces.Contains(dep);
                isKnown.Should().BeTrue(
                    $"DependsOn entry '{dep}' in group '{group.FullName}' must resolve to a known namespace");
            }
        }
    }

    #endregion

    #region ResolveOwningProject

    [Test]
    public void ResolveOwningProject_ExactMatch_ReturnsProject()
    {
        // arrange
        var rootNs = new Dictionary<string, string>
        {
            ["MyApp"] = "MyApp",
            ["MyApp.Core"] = "MyApp.Core"
        };
        var sorted = new List<string> { "MyApp.Core", "MyApp" };

        // act
        var (project, root) = ProjectDesignService.ResolveOwningProject("MyApp.Core", rootNs, sorted);

        // assert
        project.Should().Be("MyApp.Core");
        root.Should().Be("MyApp.Core");
    }

    [Test]
    public void ResolveOwningProject_ChildNamespace_MatchesLongestPrefix()
    {
        // arrange
        var rootNs = new Dictionary<string, string>
        {
            ["MyApp"] = "MyApp",
            ["MyApp.Core"] = "MyApp.Core"
        };
        var sorted = new List<string> { "MyApp.Core", "MyApp" };

        // act
        var (project, root) = ProjectDesignService.ResolveOwningProject(
            "MyApp.Core.Messaging", rootNs, sorted);

        // assert
        project.Should().Be("MyApp.Core");
        root.Should().Be("MyApp.Core");
    }

    [Test]
    public void ResolveOwningProject_NoMatch_FallsBackToNamespace()
    {
        // arrange
        var rootNs = new Dictionary<string, string> { ["MyApp"] = "MyApp" };
        var sorted = new List<string> { "MyApp" };

        // act
        var (project, root) = ProjectDesignService.ResolveOwningProject(
            "External.Lib", rootNs, sorted);

        // assert
        project.Should().Be("External.Lib");
        root.Should().Be("External.Lib");
    }

    #endregion

    #region GetRelativeNamespace

    [Test]
    public void GetRelativeNamespace_SameAsRoot_ReturnsEmpty()
    {
        // act
        var result = ProjectDesignService.GetRelativeNamespace("MyApp", "MyApp");

        // assert
        result.Should().BeEmpty();
    }

    [Test]
    public void GetRelativeNamespace_ChildOfRoot_ReturnsRelativePart()
    {
        // act
        var result = ProjectDesignService.GetRelativeNamespace("MyApp.Services.Handlers", "MyApp");

        // assert
        result.Should().Be("Services.Handlers");
    }

    [Test]
    public void GetRelativeNamespace_EmptyRoot_ReturnsFullNamespace()
    {
        // act
        var result = ProjectDesignService.GetRelativeNamespace("Some.Namespace", "");

        // assert
        result.Should().Be("Some.Namespace");
    }

    [Test]
    public void GetRelativeNamespace_DifferentRoot_ReturnsFullNamespace()
    {
        // act
        var result = ProjectDesignService.GetRelativeNamespace("Other.Namespace", "MyApp");

        // assert
        result.Should().Be("Other.Namespace");
    }

    #endregion

    #region ExtractRootNamespace

    [Test]
    public void ExtractRootNamespace_NoCsprojTag_ReturnsNull()
    {
        // arrange
        var csproj = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");
        File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup></PropertyGroup></Project>");

        try
        {
            // act
            var result = ProjectDesignService.ExtractRootNamespace(csproj);

            // assert
            result.Should().BeNull();
        }
        finally
        {
            File.Delete(csproj);
        }
    }

    [Test]
    public void ExtractRootNamespace_WithTag_ReturnsValue()
    {
        // arrange
        var csproj = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");
        File.WriteAllText(csproj,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><RootNamespace>My.Custom.Ns</RootNamespace></PropertyGroup></Project>");

        try
        {
            // act
            var result = ProjectDesignService.ExtractRootNamespace(csproj);

            // assert
            result.Should().Be("My.Custom.Ns");
        }
        finally
        {
            File.Delete(csproj);
        }
    }

    #endregion

    #region Markdown formatting

    [Test]
    public void FormatMarkdown_NoStandaloneProjectHeaders()
    {
        // act
        var result = Act();
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert — no standalone project-level headers (groups always have parenthesized full name)
        var lines = md.Split('\n').Select(l => l.Trim()).ToList();
        lines.Should().NotContain("## TestProject.Core");
        lines.Should().NotContain("## TestProject.App");
    }

    [Test]
    public void FormatMarkdown_ContainsGroupHeaders()
    {
        // act
        var result = Act();
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert
        md.Should().Contain("## Services (TestProject.Core.Services)");
        md.Should().Contain("## Persistence (TestProject.Core.Persistence)");
        md.Should().Contain("## Messaging (TestProject.App.Messaging)");
        md.Should().Contain("## Mapping (TestProject.App.Mapping)");
        md.Should().Contain("## Mapping.Tv2 (TestProject.App.Mapping.Tv2)");
        md.Should().Contain("## Mapping.Tv3 (TestProject.App.Mapping.Tv3)");
        md.Should().Contain("## Mapping.Tv4 (TestProject.App.Mapping.Tv4)");
    }

    [Test]
    public void FormatMarkdown_ShowsFullNameInHeader()
    {
        // act
        var result = Act();
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert
        md.Should().Contain("(TestProject.Core.Services)");
        md.Should().Contain("(TestProject.App.Mapping.Tv2)");
        md.Should().Contain("(TestProject.App.Services.GenericConsumers)");
        md.Should().NotContain("Full name:");
    }

    [Test]
    public void FormatMarkdown_ShowsEntriesCountLabel()
    {
        // act
        var result = Act();
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert
        md.Should().Contain("Entries count:");
        md.Should().NotContain("Abstractions:");
    }

    [Test]
    public void FormatMarkdown_ShowsDependsOnWithFullNamespaces()
    {
        // act
        var result = Act();
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert
        md.Should().Contain("- TestProject.Core.Persistence");
        md.Should().Contain("- TestProject.Core.Notifications");
        md.Should().Contain("- TestProject.Core.EventHandling");
    }

    [Test]
    public void FormatMarkdown_NoIndividualAbstractionNames()
    {
        // act
        var result = Act();
        var md = GetProjectDesignTool.FormatMarkdown(result);

        // assert
        md.Should().NotContain("IAnimalService");
        md.Should().NotContain("IMessageHandler");
    }

    #endregion

    #region NuGet abstractions — excluded from groups, included in DependsOn

    [Test]
    public void BuildFromDependencyMap_AutoMapperIMapper_NotInGroups()
    {
        // act
        var result = Act();

        // assert — AutoMapper.IMapper has no source file, must not create a group
        result.Groups.Should().NotContain(g => g.FullName == "AutoMapper");
    }

    [Test]
    public void BuildFromDependencyMap_MappingGroup_DependsOnAutoMapper()
    {
        // act
        var result = Act();

        // assert — AutoMapperAnimalMapper uses AutoMapper types (IMapperBase/IMapper),
        // so Mapping group depends on AutoMapper namespace
        var mappingGroup = result.Groups.Single(g => g.FullName == "TestProject.App.Mapping");
        mappingGroup.DependsOn.Should().Contain(d => d.StartsWith("AutoMapper"));
    }

    [Test]
    public void BuildFromDependencyMap_NuGetAbstraction_NotInGroups()
    {
        // arrange — nuget type has SourceFilePath = null
        var nugetAbstraction = new AbstractionInfo
        {
            FullName = "Acme.Sdk.IExternalService",
            Namespace = "Acme.Sdk",
            ProjectName = "Acme.Sdk",
            SourceFilePath = null,
            IsInterface = true,
            IsAbstractClass = false,
            IsStaticClass = false,
            Implementations = ["MyApp.Services.MyService"]
        };

        var sourceAbstraction = new AbstractionInfo
        {
            FullName = "MyApp.Services.IMyService",
            Namespace = "MyApp.Services",
            ProjectName = "MyApp",
            SourceFilePath = "/src/MyApp/Services/IMyService.cs",
            IsInterface = true,
            IsAbstractClass = false,
            IsStaticClass = false,
            Implementations = ["MyApp.Services.MyService"]
        };

        var impl = new ImplementationInfo(
            FullName: "MyApp.Services.MyService",
            Namespace: "MyApp.Services",
            ProjectName: "MyApp",
            SourceFilePath: "/src/MyApp/Services/MyService.cs",
            ImplementedAbstractions: ["MyApp.Services.IMyService", "Acme.Sdk.IExternalService"],
            BaseClasses: [],
            Dependencies: [new AbstractionUsage("Acme.Sdk.IExternalService", false, [])]);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [nugetAbstraction.FullName] = nugetAbstraction,
                [sourceAbstraction.FullName] = sourceAbstraction
            },
            Implementations: new Dictionary<string, ImplementationInfo>
            {
                [impl.FullName] = impl
            });

        // act
        var result = new ProjectDesignService(_dependencyMapService, new DependencyAggregator())
            .BuildFromDependencyMap(depMap, "/fake/solution.slnx");

        // assert — NuGet group must not appear in the main list
        result.Groups.Should().NotContain(g => g.FullName == "Acme.Sdk");
        result.Groups.Should().Contain(g => g.FullName == "MyApp.Services");
    }

    [Test]
    public void BuildFromDependencyMap_NuGetAbstraction_DependencyStillResolvedToItsNamespace()
    {
        // arrange
        var nugetAbstraction = new AbstractionInfo
        {
            FullName = "Acme.Sdk.IExternalService",
            Namespace = "Acme.Sdk",
            ProjectName = "Acme.Sdk",
            SourceFilePath = null,
            IsInterface = true,
            IsAbstractClass = false,
            IsStaticClass = false,
            Implementations = ["MyApp.Services.MyService"]
        };

        var sourceAbstraction = new AbstractionInfo
        {
            FullName = "MyApp.Services.IMyService",
            Namespace = "MyApp.Services",
            ProjectName = "MyApp",
            SourceFilePath = "/src/MyApp/Services/IMyService.cs",
            IsInterface = true,
            IsAbstractClass = false,
            IsStaticClass = false,
            Implementations = ["MyApp.Services.MyService"]
        };

        var consumerAbstraction = new AbstractionInfo
        {
            FullName = "MyApp.Application.IConsumer",
            Namespace = "MyApp.Application",
            ProjectName = "MyApp",
            SourceFilePath = "/src/MyApp/Application/IConsumer.cs",
            IsInterface = true,
            IsAbstractClass = false,
            IsStaticClass = false,
            Implementations = ["MyApp.Application.Consumer"]
        };

        var consumerImpl = new ImplementationInfo(
            FullName: "MyApp.Application.Consumer",
            Namespace: "MyApp.Application",
            ProjectName: "MyApp",
            SourceFilePath: "/src/MyApp/Application/Consumer.cs",
            ImplementedAbstractions: ["MyApp.Application.IConsumer"],
            BaseClasses: [],
            Dependencies: [new AbstractionUsage("Acme.Sdk.IExternalService", false, [])]);

        var depMap = new DependencyMapResult(
            Abstractions: new Dictionary<string, AbstractionInfo>
            {
                [nugetAbstraction.FullName] = nugetAbstraction,
                [sourceAbstraction.FullName] = sourceAbstraction,
                [consumerAbstraction.FullName] = consumerAbstraction
            },
            Implementations: new Dictionary<string, ImplementationInfo>
            {
                [consumerImpl.FullName] = consumerImpl
            });

        // act
        var result = new ProjectDesignService(_dependencyMapService, new DependencyAggregator())
            .BuildFromDependencyMap(depMap, "/fake/solution.slnx");

        // assert — Application group depends on Acme.Sdk namespace (NuGet), even though it's not in groups
        var appGroup = result.Groups.Single(g => g.FullName == "MyApp.Application");
        appGroup.DependsOn.Should().Contain("Acme.Sdk");

        // and Acme.Sdk itself is not a group
        result.Groups.Should().NotContain(g => g.FullName == "Acme.Sdk");
    }

    #endregion

    /// <summary>
    /// Simple IWorkspaceProvider that returns the pre-loaded CachedSolution.
    /// </summary>
    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
