using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class SymbolInfoServiceTests
{
    CachedSolution _cachedSolution = null!;
    SymbolInfoService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.LoadTestSolutionAsync();
        _sut = new SymbolInfoService(new TestWorkspaceProvider(_cachedSolution));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _cachedSolution.Dispose();

    async Task<string> Act(string typeName) =>
        await _sut.GetSymbolInfoAsync(CompilationHelper.SolutionPath, typeName);

    #region Not found

    [Test]
    public async Task GetSymbolInfoAsync_UnknownType_ReturnsNotFound()
    {
        // act
        var result = await Act("NonExistent.Type");

        // assert
        result.Should().Contain("not found");
    }

    #endregion

    #region Enum

    [Test]
    public async Task GetSymbolInfoAsync_Enum_ReturnsAllValues()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().Contain("Unknown = 0");
        result.Should().Contain("Cat = 1");
        result.Should().Contain("Dog = 2");
        result.Should().Contain("Parrot = 3");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Enum_ReturnsUnderlyingType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().Contain("Underlying type:");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Enum_DoesNotContainMemberSections()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().NotContain("Properties:");
        result.Should().NotContain("Methods:");
    }

    #endregion

    #region Constants

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithConstants_ReturnsConstants()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Constants:");
        result.Should().Contain("MaxNameLength = 100");
        result.Should().Contain("DefaultPrefix = \"Animal_\"");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalConst_ReturnsInternalConst()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("InternalBatchSize = 50");
        result.Should().MatchRegex(@"internal\s+int\s+InternalBatchSize");
    }

    #endregion

    #region Static fields

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithStaticField_ReturnsStaticFields()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Static fields:");
        result.Should().Contain("FallbackKind");
    }

    #endregion

    #region Instance fields

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPublicInstanceField_ReturnsFields()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Fields:");
        result.Should().Contain("DisplayLabel");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithReadonlyInstanceField_ShowsReadonlyModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"readonly\s+int\s+MaxRetries");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalInstanceField_ReturnsInternalField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+string\s+InternalTag");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPrivateInstanceField_DoesNotReturnPrivateField()
    {
        // act
        var result = await Act("TestProject.App.Helpers.AnimalFormatter");

        // assert
        result.Should().NotContain("_repository");
    }

    #endregion

    #region Properties

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProperties_ReturnsProperties()
    {
        // act
        var result = await Act("TestProject.Core.Models.Animal");

        // assert
        result.Should().Contain("Properties:");
        result.Should().Contain("int Id");
        result.Should().Contain("string Name");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithStaticProperty_ReturnsStaticProperties()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Static properties:");
        result.Should().Contain("MaxAllowed");
    }

    #endregion

    #region Methods

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithStaticMethod_ReturnsStaticMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Static methods:");
        result.Should().Contain("BuildDefaultName");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalStaticMethod_ReturnsInternalStaticMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+string\s+InternalFormat");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInstanceMethod_ReturnsMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Methods:");
        result.Should().Contain("InstanceMethod");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Interface_ReturnsInterfaceMethods()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().Contain("Methods:");
        result.Should().Contain("GetById");
        result.Should().Contain("GetByKind");
        result.Should().Contain("Add");
    }

    #endregion

    #region Constructors

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithConstructors_ReturnsConstructorsSection()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Constructors:");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithParameterlessConstructor_ShowsEmptyParams()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("AnimalDefaults()");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithParameterizedConstructor_ShowsParams()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("AnimalDefaults(string displayLabel)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalConstructor_ShowsInternalConstructor()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+AnimalDefaults\(string displayLabel, int maxRetries\)");
    }

    #endregion

    #region Nested types

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPublicNestedType_ReturnsNestedTypes()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("Nested types:");
        result.Should().Contain("ValidationRules");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalNestedType_ReturnsInternalNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("CacheOptions");
        result.Should().MatchRegex(@"internal\s+\[Class\]\s+.*CacheOptions");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPrivateNestedType_DoesNotReturnPrivateNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().NotContain("PrivateInner");
    }

    #endregion

    #region Source location

    [Test]
    public async Task GetSymbolInfoAsync_SourceType_ContainsSourceFileInfo()
    {
        // act
        var result = await Act("TestProject.Core.Models.Animal");

        // assert
        result.Should().Contain("source:");
        result.Should().Contain("Animal.cs");
    }

    #endregion

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
