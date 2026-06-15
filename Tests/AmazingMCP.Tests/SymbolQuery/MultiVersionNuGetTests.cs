using AmazingMCP.Configuration;
using AmazingMCP.Services.Decompile;
using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using static AmazingMCP.Tests.Helpers.CompilationHelper;

namespace AmazingMCP.Tests.SymbolQuery;

/// <summary>
/// Tests verifying multi-version NuGet type detection and version banner rendering.
/// TestSolution has Microsoft.Extensions.Options 10.0.5 (Infrastructure) and 10.0.8 (App),
/// both with AssemblyVersion=10.0.0.0 — the hardest case for version discrimination.
/// </summary>
/// <remarks>
/// Regression guard: if version resolution is keyed by AssemblyIdentity instead of file path,
/// both versions collapse into one and these tests fail.
/// </remarks>
[Parallelizable(ParallelScope.Self)]
public class MultiVersionNuGetTests
{
    // Microsoft.Extensions.Options: 10.0.5 (Infrastructure) + 10.0.8 (App)
    // Both have AssemblyVersion=10.0.0.0 — same identity, different NuGet versions.
    const string MultiVersionType = "Microsoft.Extensions.Options.OptionsValidationException";
    const string Version105 = "10.0.5";
    const string Version108 = "10.0.8";

    SymbolQueryService _queryService = null!;
    SymbolInfoService _infoService = null!;
    DecompileTypeService _decompileService = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        var workspaceProvider = CreateWorkspaceProvider(cachedSolution);
        var roslyn = new RoslynSymbolService(
            workspaceProvider,
            new WildcardPatternFactory(),
            CreateTypeProvider(),
            CompilationHelper.CreateVersionedStrategy());

        _queryService = new SymbolQueryService(roslyn, Options.Create(new SymbolOptions()));

        _infoService = new SymbolInfoService(
            roslyn,
            workspaceProvider,
            new RoslynDerivedTypeService(CreateTypeProvider(), CreateAllInstancesStrategy()),
            new XmlDocExtractor(),
            new WildcardPatternFactory())
        {
            CompactModeThreshold = 200
        };

        _decompileService = new DecompileTypeService(
            roslyn,
            workspaceProvider,
            new FilteredSourceService(new FileStructureService(), new WildcardPatternFactory()),
            new SourceDigestService(new XmlDocExtractor()),
            Options.Create(new ReadCsOptions { ReadOutputMaxLength = 50_000 }));
    }

    // ── query_symbol: single NuGet version ───────────────────────────────────

    [Test]
    public async Task QueryAsync_SingleVersionNuGetType_ShowsVersionInParens()
    {
        // act — AutoMapper exists only in one version in TestSolution
        var result = await _queryService.QueryAsync(CompilationHelper.SolutionPath, "AutoMapper.MapperConfiguration");

        // assert
        result.Should().MatchRegex(@"\(assembly: AutoMapper \[v\d+\.\d+\.\d+\]\)");
    }

    [Test]
    public async Task QueryAsync_SourceType_NoVersionInParens()
    {
        // act
        var result = await _queryService.QueryAsync(CompilationHelper.SolutionPath, "TestProject.Core.Models.Animal");

        // assert — source types have no version suffix
        result.Should().NotMatchRegex(@", v\d");
        result.Should().Contain("source:");
    }

    // ── query_symbol: multiple versions ──────────────────────────────────────

    [Test]
    public async Task QueryAsync_MultiVersionNuGetType_ReturnsBothVersions()
    {
        // act
        var result = await _queryService.QueryAsync(CompilationHelper.SolutionPath, MultiVersionType);

        // assert
        result.Should().Contain(Version105);
        result.Should().Contain(Version108);
    }

    // ── get_type_details: version banner ──────────────────────────────────────

    [Test]
    public async Task GetTypeDetailsAsync_MultiVersionNuGetType_ShowsVersionWarningBanner()
    {
        // act
        var result = await _infoService.GetTypeDetailsAsync(CompilationHelper.SolutionPath, MultiVersionType);

        // assert
        result.Should().Contain("WARNING");
        result.Should().Contain(Version105);
        result.Should().Contain(Version108);
    }

    [Test]
    public async Task GetTypeDetailsAsync_MultiVersionNuGetType_DefaultsToHighestVersion()
    {
        // act
        var result = await _infoService.GetTypeDetailsAsync(CompilationHelper.SolutionPath, MultiVersionType);

        // assert — banner shows which version is displayed
        result.Should().Contain($"Showing version: {Version108}");
    }

    [Test]
    public async Task GetTypeDetailsAsync_MultiVersionNuGetType_WithVersionParam_ShowsRequestedVersion()
    {
        // act
        var result = await _infoService.GetTypeDetailsAsync(
            CompilationHelper.SolutionPath, MultiVersionType, version: Version105);

        // assert
        result.Should().Contain($"Showing version: {Version105}");
        result.Should().NotContain($"Showing version: {Version108}");
    }

    // ── decompile_type: version banner ────────────────────────────────────────

    [Test]
    public async Task DecompileTypeAsync_MultiVersionNuGetType_ShowsVersionWarningBanner()
    {
        // act
        var result = await _decompileService.DecompileTypeAsync(
            CompilationHelper.SolutionPath, MultiVersionType);

        // assert
        result.Should().Contain("WARNING");
        result.Should().Contain(Version105);
        result.Should().Contain(Version108);
    }

    [Test]
    public async Task DecompileTypeAsync_MultiVersionNuGetType_DefaultsToHighestVersion()
    {
        // act
        var result = await _decompileService.DecompileTypeAsync(
            CompilationHelper.SolutionPath, MultiVersionType);

        // assert
        result.Should().Contain($"Showing version: {Version108}");
    }

    [Test]
    public async Task DecompileTypeAsync_MultiVersionNuGetType_WithVersionParam_ShowsRequestedVersion()
    {
        // act
        var result = await _decompileService.DecompileTypeAsync(
            CompilationHelper.SolutionPath, MultiVersionType, version: Version105);

        // assert
        result.Should().Contain($"Showing version: {Version105}");
        result.Should().NotContain($"Showing version: {Version108}");
    }
}
