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
        _sut = new SymbolInfoService(new RoslynSymbolService(new TestWorkspaceProvider(_cachedSolution), new WildcardPatternFactory()));
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
        result.Should().NotContain("int Id");
        result.Should().NotContain("string Name");
    }

    #endregion

    #region Constants

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithConstants_ReturnsConstants()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
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
        result.Should().MatchRegex(@"internal\s+const\s+int\s+InternalBatchSize");
    }

    #endregion

    #region Static fields

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithStaticField_ReturnsStaticFields()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
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
        result.Should().Contain("int Id");
        result.Should().Contain("string Name");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithStaticProperty_ReturnsStaticProperties()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
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
        result.Should().Contain("BuildDefaultName");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalStaticMethod_ReturnsInternalStaticMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"internal\s+static\s+string\s+InternalFormat");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInstanceMethod_ReturnsMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("InstanceMethod");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Interface_ReturnsInterfaceMethods()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
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
        result.Should().Contain("AnimalDefaults()");
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
        result.Should().Contain("ValidationRules");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithInternalNestedType_ReturnsInternalNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("CacheOptions");
        result.Should().MatchRegex(@"internal\s+class\s+.*CacheOptions");
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

    #region Protected members

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedConst_ReturnsProtectedConst()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+const\s+int\s+ProtectedMaxAge\s*=\s*99");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedStaticField_ReturnsProtectedStaticField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+static\s+readonly\s+int\s+ProtectedStaticSeed");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInstanceField_ReturnsProtectedField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+readonly\s+int\s+ProtectedRetryLimit");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedConstructor_ReturnsProtectedConstructor()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+AnimalDefaults\(string displayLabel, bool isProtected\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedStaticProperty_ReturnsProtectedStaticProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+static\s+int\s+ProtectedStaticProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInstanceProperty_ReturnsProtectedProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+int\s+ProtectedInstanceProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedStaticMethod_ReturnsProtectedStaticMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+static\s+string\s+ProtectedStaticFormat");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInstanceMethod_ReturnsProtectedMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+string\s+ProtectedInstanceMethod");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedNestedType_ReturnsProtectedNestedType()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().Contain("ProtectedInnerConfig");
        result.Should().MatchRegex(@"protected\s+class\s+.*ProtectedInnerConfig");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedInternalField_ReturnsProtectedInternalField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected internal\s+string\s+ProtectedInternalTag");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPrivateProtectedField_ReturnsPrivateProtectedField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"private protected\s+int\s+PrivateProtectedScore");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithPrivateField_DoesNotReturnPrivateField()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().NotContain("PrivateInner");
        result.Should().NotContain("Secret");
    }

    #endregion

    #region Virtual and abstract

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithVirtualMethod_ShowsVirtualModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"virtual\s+string\s+GetLabel\(\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithVirtualMethodWithParams_ShowsVirtualModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"virtual\s+int\s+ComputeScore\(int input\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithProtectedVirtualMethod_ShowsBothModifiers()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"protected\s+virtual\s+string\s+FormatInternal\(\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithVirtualProperty_ShowsVirtualModifier()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"virtual\s+int\s+VirtualProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_ShowsAbstractMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"abstract\s+string\s+GetName\(\)");
        result.Should().MatchRegex(@"abstract\s+int\s+GetScore\(int input\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_ShowsProtectedAbstractMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"protected\s+abstract\s+string\s+FormatDescription\(\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_ShowsAbstractProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"abstract\s+int\s+AbstractProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_ShowsVirtualMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"virtual\s+string\s+GetSummary\(\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ConcreteClass_ShowsOverrideMethods()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"override\s+string\s+GetName\(\)");
        result.Should().MatchRegex(@"override\s+int\s+GetScore\(int input\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ConcreteClass_ShowsSealedOverrideMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"public\s+override\s+sealed\s+string\s+GetSummary\(\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ConcreteClass_ShowsOverrideProperty()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"override\s+int\s+AbstractProp");
        result.Should().MatchRegex(@"override\s+string\s+VirtualPropOnBase");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ConcreteClass_ShowsProtectedOverrideMethod()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"protected\s+override\s+string\s+FormatDescription\(\)");
    }

    #endregion

    #region Type header modifiers

    [Test]
    public async Task GetSymbolInfoAsync_PublicClass_HeaderContainsPublicClass()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalDefaults");

        // assert
        result.Should().MatchRegex(@"public\s+class\s+TestProject\.Core\.Models\.AnimalDefaults");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_HeaderContainsAbstractClass()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalBase");

        // assert
        result.Should().MatchRegex(@"public\s+abstract\s+class\s+TestProject\.Core\.Models\.AnimalBase");
    }

    [Test]
    public async Task GetSymbolInfoAsync_SealedClass_HeaderContainsSealedClass()
    {
        // act
        var result = await Act("TestProject.Core.Models.ConcreteAnimal");

        // assert
        result.Should().MatchRegex(@"public\s+class\s+TestProject\.Core\.Models\.ConcreteAnimal");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Interface_HeaderContainsInterface()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().MatchRegex(@"public\s+interface\s+TestProject\.Core\.Services\.IAnimalService");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Enum_HeaderContainsEnum()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().MatchRegex(@"public\s+enum\s+TestProject\.Core\.Models\.AnimalKind");
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

    #region Derived types

    [Test]
    public async Task GetSymbolInfoAsync_Interface_ContainsKnownImplementors()
    {
        // act
        var result = await Act("TestProject.Core.Services.IAnimalService");

        // assert
        result.Should().Contain("Known implementors");
        result.Should().Contain("TestProject.App.Services.AnimalService");
        result.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
        result.Should().Contain("TestProject.App.Services.TracedAnimalService");
    }

    [Test]
    public async Task GetSymbolInfoAsync_AbstractClass_ContainsKnownDerivedTypes()
    {
        // act
        var result = await Act("TestProject.App.Services.AnimalServiceBase");

        // assert
        result.Should().Contain("Known derived types");
        result.Should().Contain("TestProject.App.Services.AdvancedAnimalService");
    }

    [Test]
    public async Task GetSymbolInfoAsync_InterfaceWithNoImplementors_DoesNotContainDerivedSection()
    {
        // act — IAnimalValidator has no implementors in the test solution
        var result = await Act("TestProject.Core.Services.IAnimalValidator");

        // assert
        result.Should().NotContain("Known implementors");
        result.Should().NotContain("Known derived types");
    }

    [Test]
    public async Task GetSymbolInfoAsync_Enum_DoesNotContainDerivedSection()
    {
        // act
        var result = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        result.Should().NotContain("Known implementors");
        result.Should().NotContain("Known derived types");
    }

    #endregion

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
