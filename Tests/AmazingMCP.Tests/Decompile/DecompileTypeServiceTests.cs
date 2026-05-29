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

namespace AmazingMCP.Tests.Decompile;

[Parallelizable(ParallelScope.Self)]
public class DecompileTypeServiceTests
{
    DecompileTypeService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new DecompileTypeService(
            new RoslynSymbolService(CreateWorkspaceProvider(cachedSolution), new WildcardPatternFactory(), CreateTypeProvider(), CompilationHelper.CreateVersionedStrategy()),
            new FilteredSourceService(new FileStructureService(), new WildcardPatternFactory()),
            new SourceDigestService(new XmlDocExtractor()),
            Options.Create(new ReadCsOptions { ReadOutputMaxLength = 50_000 }));
    }

    async Task<string> Act(string typeName, string[]? memberFilters = null) =>
        await _sut.DecompileTypeAsync(CompilationHelper.SolutionPath, typeName, memberFilters);

    // --- full decompilation ---

    [Test]
    public async Task DecompileTypeAsync_NuGetType_ReturnsUsings()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration");

        // assert
        result.Should().Contain("using ");
    }

    [Test]
    public async Task DecompileTypeAsync_NuGetType_ReturnsTypeDeclaration()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration");

        // assert
        result.Should().Contain("class MapperConfiguration");
    }

    [Test]
    public async Task DecompileTypeAsync_NuGetType_ReturnsConstructor()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration");

        // assert
        result.Should().Contain("MapperConfiguration(");
    }

    [Test]
    public async Task DecompileTypeAsync_NuGetType_ReturnsInstanceMethod()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration");

        // assert
        result.Should().Contain("CreateMapper");
    }

    [Test]
    public async Task DecompileTypeAsync_NuGetType_ReturnsStaticMethod()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration");

        // assert
        result.Should().Contain("GetMappingError");
    }

    [Test]
    public async Task DecompileTypeAsync_StaticTypeWithConstantField_ReturnsField()
    {
        // act
        var result = await Act("Microsoft.Extensions.Options.Options");

        // assert
        result.Should().Contain("DefaultName");
    }

    // --- member filter ---

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_ReturnsOnlyMatchingMembers()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration", ["*CreateMapper*"]);

        // assert
        result.Should().Contain("CreateMapper");
        result.Should().NotContain("AssertConfigurationIsValid");
        result.Should().NotContain("CompileMappings");
    }

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_AlwaysIncludesConstructors()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration", ["*CreateMapper*"]);

        // assert
        result.Should().Contain("MapperConfiguration(");
    }

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_IncludesUsingsAndTypeHeader()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration", ["*CreateMapper*"]);

        // assert
        result.Should().Contain("using ");
        result.Should().Contain("class MapperConfiguration");
    }

    [Test]
    public async Task DecompileTypeAsync_WithWildcardFilter_MatchesMultipleMembers()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration", ["*Assert*", "*Compile*"]);

        // assert
        result.Should().Contain("AssertConfigurationIsValid");
        result.Should().Contain("CompileMappings");
        result.Should().NotContain("CreateMapper");
    }

    [Test]
    public async Task DecompileTypeAsync_WithFilterMatchingNothing_ReturnsOnlyConstructors()
    {
        // act
        var result = await Act("AutoMapper.MapperConfiguration", ["*NonExistentXyz*"]);

        // assert
        result.Should().Contain("MapperConfiguration(");
        result.Should().NotContain("CreateMapper");
        result.Should().NotContain("AssertConfigurationIsValid");
    }

    // --- type definition integrity with filters ---

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_TypeXmlDocIsPresent()
    {
        // act
        var result = await Act("AutoMapper.AutoMapperMappingException", ["*Message*"]);

        // assert
        result.Should().Contain("Wraps mapping exceptions");
    }

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_TypeHeaderIsPresent()
    {
        // act
        var result = await Act("AutoMapper.AutoMapperMappingException", ["*Message*"]);

        // assert
        result.Should().Contain("class AutoMapperMappingException");
    }

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_UsingsArePresent()
    {
        // act
        var result = await Act("AutoMapper.AutoMapperMappingException", ["*Message*"]);

        // assert
        result.Should().Contain("using ");
    }

    [Test]
    public async Task DecompileTypeAsync_WithMemberFilter_BaseTypeIsInHeader()
    {
        // act
        var result = await Act("AutoMapper.AutoMapperMappingException", ["*Message*"]);

        // assert
        result.Should().Contain("Exception");
    }

    // --- generic types ---

    [Test]
    public async Task DecompileTypeAsync_OpenGenericNuGetType_ReturnsTypeDeclaration()
    {
        // act
        var result = await Act("Microsoft.Extensions.Options.OptionsManager<TOptions>");

        // assert
        result.Should().Contain("class OptionsManager");
    }

    [Test]
    public async Task DecompileTypeAsync_OpenGenericNuGetType_ReturnsInstanceMember()
    {
        // act
        var result = await Act("Microsoft.Extensions.Options.OptionsManager<TOptions>");

        // assert
        result.Should().Contain("Get");
    }

    [Test]
    public async Task DecompileTypeAsync_OpenGenericTwoTypeParameters_ReturnsTypeDeclaration()
    {
        // act
        var result = await Act("AutoMapper.IMappingExpression<TSource, TDestination>");

        // assert
        result.Should().Contain("interface IMappingExpression");
    }

    [Test]
    public async Task DecompileTypeAsync_OpenGenericNuGetType_BacktickNotation_ReturnsTypeDeclaration()
    {
        // act
        var result = await Act("Microsoft.Extensions.Options.OptionsManager`1");

        // assert
        result.Should().Contain("class OptionsManager");
    }

    [Test]
    public async Task DecompileTypeAsync_OpenGenericNuGetType_WithMemberFilter_ReturnsFilteredMembers()
    {
        // act
        var result = await Act("Microsoft.Extensions.Options.OptionsManager<TOptions>", ["*Get*"]);

        // assert
        result.Should().Contain("class OptionsManager");
        result.Should().Contain("Get");
    }

    // --- special messages ---

    [Test]
    public async Task DecompileTypeAsync_SourceType_ReturnsErrorWithSourcePaths()
    {
        // act
        var result = await Act("TestProject.Core.Models.Animal");

        // assert
        result.Should().Contain("defined in source");
        result.Should().Contain("Animal.cs");
    }

    [Test]
    public async Task DecompileTypeAsync_TypeNotFound_ReturnsNotFoundError()
    {
        // act
        var result = await Act("NonExistent.Type.That.DoesNotExist");

        // assert
        result.Should().Contain("not found");
    }

    [Test]
    public async Task DecompileTypeAsync_LargeTypeWithoutFilter_ReturnsWarningWhenExceedsLimit()
    {
        // arrange
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        var sut = new DecompileTypeService(
            new RoslynSymbolService(CreateWorkspaceProvider(cachedSolution), new WildcardPatternFactory(), CreateTypeProvider(), CompilationHelper.CreateVersionedStrategy()),
            new FilteredSourceService(new FileStructureService(), new WildcardPatternFactory()),
            new SourceDigestService(new XmlDocExtractor()),
            Options.Create(new ReadCsOptions { ReadOutputMaxLength = 10 }));

        // act
        var result = await sut.DecompileTypeAsync(CompilationHelper.SolutionPath, "AutoMapper.MapperConfiguration");

        // assert
        result.Should().Contain("too large");
        result.Should().Contain("memberFilters");
    }
}
