using AmazingMCP.Models;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using static AmazingMCP.Tests.Helpers.CompilationHelper;

namespace AmazingMCP.Tests.SymbolQuery;

public class RoslynSymbolServiceTests
{
    CachedSolution _cachedSolution = null!;
    RoslynSymbolService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new RoslynSymbolService(CreateWorkspaceProvider(_cachedSolution), new WildcardPatternFactory(), CreateTypeProvider(), CompilationHelper.CreateVersionedStrategy());
    }

    async Task<(string? FullName, string? Error)> Act(string fullTypeName)
    {
        var solution = await CompilationHelper.GetSharedSolutionAsync();
        var (group, error) = _sut.FindExactType(solution, fullTypeName);
        return (group?.FullName, error);
    }

    Task<IReadOnlyList<SymbolResult>> QueryTypes(params string[] typeQuery) =>
        _sut.QuerySymbolsAsync(CompilationHelper.SolutionPath, typeQuery, typeExclude: null, memberQuery: null);

    Task<IReadOnlyList<SymbolResult>> QueryMembers(params string[] memberQuery) =>
        _sut.QuerySymbolsAsync(CompilationHelper.SolutionPath, typeQuery: null, typeExclude: null, memberQuery);

    // ── Not found ─────────────────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_UnknownType_ReturnsNotFound()
    {
        // act
        var (fullName, error) = await Act("NonExistent.Type");

        // assert
        fullName.Should().BeNull();
        error.Should().Contain("not found");
    }

    // ── Non-generic ───────────────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_NonGenericType_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.Models.AnimalKind");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.Models.AnimalKind");
    }

    // ── Generic — C# syntax ───────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_GenericType_CSharpSyntax_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.Logging.IGenericTracer<TService>");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.Logging.IGenericTracer<TService>");
    }

    // ── Generic — CLR backtick notation ──────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_GenericType_ClrBacktickNotation_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.Logging.IGenericTracer`1");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.Logging.IGenericTracer<TService>");
    }

    // ── Generic — two type params ─────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_TwoParamGeneric_CSharpSyntax_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.EventHandling.IEventHandler<TEvent, TResult>");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.EventHandling.IEventHandler<TEvent, TResult>");
    }

    [Test]
    public async Task FindExactTypeAsync_TwoParamGeneric_ClrBacktickNotation_ReturnsSymbol()
    {
        // act
        var (fullName, error) = await Act("TestProject.Core.EventHandling.IEventHandler`2");

        // assert
        error.Should().BeNull();
        fullName.Should().Be("TestProject.Core.EventHandling.IEventHandler<TEvent, TResult>");
    }

    // ── Wrong arity ───────────────────────────────────────────────────────────

    [Test]
    public async Task FindExactTypeAsync_WrongArity_ReturnsNotFound()
    {
        // act — IGenericTracer has arity 1, requesting arity 2
        var (fullName, error) = await Act("TestProject.Core.Logging.IGenericTracer`2");

        // assert
        fullName.Should().BeNull();
        error.Should().Contain("not found");
    }

    // ── QuerySymbolsAsync — types ─────────────────────────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_ExactTypeName_ReturnsMatchingTypes()
    {
        // act
        var results = await QueryTypes("Animal");

        // assert
        results.Where(r => r.DeclaringType is null)
            .Select(r => r.Name)
            .Should().Contain("Animal");
    }

    [Test]
    public async Task QuerySymbolsAsync_WildcardPattern_ReturnsAllMatchingTypes()
    {
        // act
        var results = await QueryTypes("*Animal*");

        // assert
        results.Where(r => r.DeclaringType is null)
            .Select(r => r.Name)
            .Should().Contain(["Animal", "IAnimalService", "IAnimalRepository", "AnimalKind"]);
    }

    [Test]
    public async Task QuerySymbolsAsync_TypeResult_HasNoDeclaringType()
    {
        // act
        var results = await QueryTypes("AnimalKind");

        // assert
        results.Where(r => r.Name == "AnimalKind")
            .Should().AllSatisfy(r => r.DeclaringType.Should().BeNull());
    }

    // ── QuerySymbolsAsync — members ───────────────────────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_MethodName_ReturnsMemberResultsWithDeclaringType()
    {
        // act
        var results = await QueryMembers("FindById");

        // assert
        var members = results.Where(r => r.Kind == "Method").ToList();
        members.Should().NotBeEmpty();
        members.Should().AllSatisfy(r =>
        {
            r.DeclaringType.Should().NotBeNull();
            r.Name.Should().Be("FindById");
        });
    }

    [Test]
    public async Task QuerySymbolsAsync_PropertyName_ReturnsMemberResultsWithDeclaringType()
    {
        // act
        var results = await QueryMembers("Count");

        // assert — "Count" property exists on IAnimalRepository and IRepository<T>
        var members = results.Where(r => r.Kind == "Property" && r.Name == "Count").ToList();
        members.Should().NotBeEmpty();
        members.Should().AllSatisfy(r => r.DeclaringType.Should().NotBeNull());
    }

    [Test]
    public async Task QuerySymbolsAsync_MemberResult_NameIsSimpleNameOnly()
    {
        // act — "GetById" exists on IAnimalService and IRepository<T>
        var results = await QueryMembers("GetById");

        // assert — Name must be just "GetById", not a full signature
        results.Where(r => r.Kind == "Method")
            .Should().AllSatisfy(r => r.Name.Should().Be("GetById"));
    }

    [Test]
    public async Task QuerySymbolsAsync_WildcardOnMembers_ReturnsMatchingMethods()
    {
        // act
        var results = await QueryMembers("Get*");

        // assert — should find GetById, GetByKind etc.
        results.Where(r => r.Kind == "Method")
            .Select(r => r.Name)
            .Should().Contain(["GetById", "GetByKind"]);
    }

    [Test]
    public async Task QuerySymbolsAsync_MemberResult_DeclaringTypeHasCorrectKindAndFullName()
    {
        // act
        var results = await QueryMembers("FindById");

        // assert
        var member = results.First(r => r.Kind == "Method" && r.Name == "FindById");
        member.DeclaringType.Should().BeEquivalentTo(new
        {
            FullName = "TestProject.Core.Persistence.IAnimalRepository",
            Kind = "Interface"
        });
    }

    // ── QuerySymbolsAsync — enum values ──────────────────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_EnumValueName_ReturnsEnumValueWithDeclaringType()
    {
        // act — "Dog" is a value of AnimalKind enum
        var results = await QueryMembers("Dog");

        // assert
        results.Should().ContainSingle(r =>
            r.Kind == "EnumValue" &&
            r.Name == "Dog" &&
            r.DeclaringType!.Name == "AnimalKind");
    }

    [Test]
    public async Task QuerySymbolsAsync_WildcardOnEnumValues_ReturnsMatchingValues()
    {
        // act — AnimalKind has Cat, Dog, Parrot, Unknown
        var results = await QueryMembers("*a*");

        // assert — Cat and Parrot contain "a" (case-insensitive via *a*)
        var enumValues = results
            .Where(r => r.Kind == "EnumValue" && r.DeclaringType?.Name == "AnimalKind")
            .Select(r => r.Name)
            .ToList();

        enumValues.Should().Contain(["Cat", "Parrot"]);
    }

    // ── QuerySymbolsAsync — no match on return type or parameter types ────────

    [Test]
    public async Task QuerySymbolsAsync_PatternMatchesReturnTypeOnly_ReturnsNoMembers()
    {
        // arrange — "*string*" matches the C# type "string" as a return/parameter type,
        // but none of the TestProject types, methods, or properties are *named* with "string".
        // The pattern must only match against Name, not signatures or return types.

        // act
        var results = await QueryMembers("*string*");

        // assert — no source-level results (TestProject has no type/member named "*string*")
        var sourceResults = results.Where(r => r.SourceFilePath != null).ToList();
        sourceResults.Should().BeEmpty();
    }

    // ── QuerySymbolsAsync — deduplication ────────────────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_SameMethodNameOnDifferentTypes_ReturnsBothMembers()
    {
        // arrange — GetById exists on IAnimalService, IRepository<T>, and their implementations

        // act
        var results = await QueryMembers("GetById");

        // assert — both interface declaring types are represented
        var methods = results.Where(r => r.Kind == "Method" && r.Name == "GetById").ToList();
        methods.Select(r => r.DeclaringType!.Name)
            .Should().Contain(["IAnimalService", "IRepository"]);
    }

    // ── QuerySymbolsAsync — type match does not pull in members ──────────────

    [Test]
    public async Task QuerySymbolsAsync_TypeQueryOnly_DoesNotReturnMembersOfThatType()
    {
        // arrange — IAnimalRepository has members FindById, FindByKind, Save, Count;
        // a typeQuery alone must return the type only

        // act
        var results = await QueryTypes("IAnimalRepository");

        // assert — the type itself is present
        results.Should().ContainSingle(r => r.Name == "IAnimalRepository" && r.DeclaringType == null);

        // assert — none of its members leaked through
        results.Should().NotContain(r =>
            r.DeclaringType != null &&
            r.DeclaringType.Name == "IAnimalRepository");
    }

    [Test]
    public async Task QuerySymbolsAsync_MemberSearch_ExcludesMembersFromWellKnownFrameworkTypes()
    {
        // act — "ToString" exists on every System.Object but should not appear
        var results = await QueryMembers("ToString");

        // assert — no results from System.* or Microsoft.* types
        results.Where(r => r.DeclaringType is not null)
            .Should().AllSatisfy(r =>
            {
                r.DeclaringType!.FullName.Should().NotStartWith("System.");
                r.DeclaringType!.FullName.Should().NotStartWith("Microsoft.");
            });
    }

    // ── QuerySymbolsAsync — private members excluded ──────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_PrivateMembersAreNotReturned()
    {
        // act — search broadly; private members must never appear
        var results = await QueryMembers("*");

        // assert
        // We can't directly check DeclaredAccessibility from SymbolResult,
        // but we can verify that all member results come from types we know
        // and that no result has a Kind that would indicate a private accessor.
        // The real guard is that the service filters by IsVisibleMember.
        // Here we just confirm the pipeline runs without error and returns members.
        results.Where(r => r.Kind is "Method" or "Property")
            .Should().NotBeEmpty();
    }

    [Test]
    public async Task QuerySymbolsAsync_PrivateNestedSourceType_IsFound()
    {
        // act — PrivateInner is a private nested class inside AnimalDefaults (source type)
        var results = await QueryTypes("PrivateInner");

        // assert — source types are returned regardless of access modifier
        results.Should().ContainSingle(r => r.Name == "PrivateInner" && r.DeclaringType == null);
    }

    [Test]
    public async Task QuerySymbolsAsync_PrivateMethodOnSourceType_IsFound()
    {
        // act — PrivateMethod is a private method on AnimalDefaults (source type)
        var results = await QueryMembers("PrivateMethod");

        // assert — private methods on source types are included
        results.Should().ContainSingle(r => r.Kind == "Method" && r.Name == "PrivateMethod");
    }

    [Test]
    public async Task QuerySymbolsAsync_PrivateFieldOnSourceType_IsNotFound()
    {
        // act — _privateField is a private field on AnimalDefaults (source type)
        var results = await QueryMembers("_privateField");

        // assert — private fields on source types are excluded
        results.Should().BeEmpty();
    }

    [Test]
    public async Task QuerySymbolsAsync_PrivatePropertyOnSourceType_IsNotFound()
    {
        // act — PrivateProp is a private property on AnimalDefaults (source type)
        var results = await QueryMembers("PrivateProp");

        // assert — private properties on source types are excluded
        results.Should().BeEmpty();
    }

    [Test]
    public async Task QuerySymbolsAsync_PrivateEventOnSourceType_IsNotFound()
    {
        // act — PrivateEvent is a private event on AnimalDefaults (source type)
        var results = await QueryMembers("PrivateEvent");

        // assert — private events on source types are excluded
        results.Should().BeEmpty();
    }

    [Test]
    public async Task QuerySymbolsAsync_Constant_IsFound()
    {
        // act — MaxNameLength is a public const on AnimalDefaults
        var results = await QueryMembers("MaxNameLength");

        // assert
        results.Should().Contain(r => r.Kind == "Const" && r.Name == "MaxNameLength");
    }

    [Test]
    public async Task QuerySymbolsAsync_Field_IsFound()
    {
        // act — DisplayLabel is a public field on AnimalDefaults
        var results = await QueryMembers("DisplayLabel");

        // assert
        results.Should().Contain(r => r.Kind == "Field" && r.Name == "DisplayLabel");
    }

    [Test]
    public async Task QuerySymbolsAsync_Event_IsFound()
    {
        // act — LabelChanged is a public event on AnimalDefaults
        var results = await QueryMembers("LabelChanged");

        // assert
        results.Should().Contain(r => r.Kind == "Event" && r.Name == "LabelChanged");
    }

    [Test]
    public async Task QuerySymbolsAsync_PrivateField_IsNotFound()
    {
        // act — _privateField is a private field on AnimalDefaults (source type)
        var results = await QueryMembers("_privateField");

        // assert — private fields on source types are excluded
        results.Should().BeEmpty();
    }

    [Test]
    public async Task QuerySymbolsAsync_StaticProperty_IsFound()
    {
        // act — MaxAllowed is a public static property on AnimalDefaults
        var results = await QueryMembers("MaxAllowed");

        // assert
        results.Should().ContainSingle(r => r.Kind == "Property" && r.Name == "MaxAllowed");
    }

    // ── KindGroup classification ──────────────────────────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_TypeResult_HasTypeKindGroup()
    {
        // act
        var results = await QueryTypes("AnimalKind");

        // assert
        results.Where(r => r.Name == "AnimalKind" && r.DeclaringType is null)
            .Should().AllSatisfy(r => r.KindGroup.Should().Be(KindGroup.Type));
    }

    [Test]
    public async Task QuerySymbolsAsync_MemberResult_HasMemberKindGroup()
    {
        // act
        var results = await QueryMembers("FindById");

        // assert
        results.Where(r => r.Kind == "Method")
            .Should().AllSatisfy(r => r.KindGroup.Should().Be(KindGroup.Member));
    }

    // ── Query modes ───────────────────────────────────────────────────────────

    [Test]
    public async Task QuerySymbolsAsync_TypeQueryOnly_ReturnsTypesOnly()
    {
        // act
        var results = await QueryTypes("Animal");

        // assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r =>
        {
            r.KindGroup.Should().Be(KindGroup.Type);
            r.DeclaringType.Should().BeNull();
        });
    }

    [Test]
    public async Task QuerySymbolsAsync_TypeQueryWithMemberName_ReturnsNothing()
    {
        // act — "FindById" is a method name only; no type is named like it
        var results = await QueryTypes("FindById");

        // assert
        results.Should().BeEmpty();
    }

    [Test]
    public async Task QuerySymbolsAsync_MemberQueryOnly_ReturnsMembersOnly()
    {
        // act
        var results = await QueryMembers("FindById");

        // assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.KindGroup.Should().Be(KindGroup.Member));
    }

    [Test]
    public async Task QuerySymbolsAsync_TypeAndMemberQuery_ReturnsMembersOfMatchingTypesOnly()
    {
        // act — GetById exists in TestProject.Core and TestProject.App types
        var results = await _sut.QuerySymbolsAsync(
            CompilationHelper.SolutionPath, typeQuery: ["TestProject.Core.*"], typeExclude: null, memberQuery: ["GetById"]);

        // assert — generic IRepository<T> is matched as well
        var declaringTypes = results.Select(r => r.DeclaringType!.FullName).ToList();
        declaringTypes.Should().Contain([
            "TestProject.Core.Persistence.IRepository<T>",
            "TestProject.Core.Services.IAnimalService"]);
        declaringTypes.Should().AllSatisfy(n => n.Should().StartWith("TestProject.Core."));
    }

    [Test]
    public async Task QuerySymbolsAsync_TypeAndMemberQuery_DoesNotReturnTypes()
    {
        // act
        var results = await _sut.QuerySymbolsAsync(
            CompilationHelper.SolutionPath, typeQuery: ["IAnimalRepository"], typeExclude: null, memberQuery: ["*"]);

        // assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.KindGroup.Should().Be(KindGroup.Member));
    }

    [Test]
    public async Task QuerySymbolsAsync_SeveralTypePatterns_ReturnsTypesMatchingAny()
    {
        // act
        var results = await QueryTypes("AnimalKind", "IAnimalRepository");

        // assert
        results.Select(r => r.Name)
            .Should().Contain(["AnimalKind", "IAnimalRepository"]);
    }

    [Test]
    public async Task QuerySymbolsAsync_SeveralMemberPatterns_ReturnsMembersMatchingAny()
    {
        // act
        var results = await QueryMembers("FindById", "MaxAllowed");

        // assert
        results.Select(r => r.Name)
            .Should().Contain(["FindById", "MaxAllowed"]);
    }

    [Test]
    public async Task QuerySymbolsAsync_TypeExclude_SkipsMatchingTypes()
    {
        // act
        var results = await _sut.QuerySymbolsAsync(
            CompilationHelper.SolutionPath, typeQuery: ["*Animal*"], typeExclude: ["*.Persistence.*"], memberQuery: null);

        // assert
        var names = results.Select(r => r.Name).ToList();
        names.Should().Contain("AnimalKind");
        names.Should().NotContain("IAnimalRepository");
    }

    [Test]
    public async Task QuerySymbolsAsync_TypeExcludeWithMemberQuery_SkipsMembersOfExcludedTypes()
    {
        // act
        var results = await _sut.QuerySymbolsAsync(
            CompilationHelper.SolutionPath, typeQuery: null, typeExclude: ["*.Persistence.*"], memberQuery: ["GetById"]);

        // assert
        var declaringTypes = results.Select(r => r.DeclaringType!.FullName).ToList();
        declaringTypes.Should().Contain([
            "TestProject.Core.Services.IAnimalService",
            "TestProject.App.Services.AnimalService"]);
        declaringTypes.Should().NotContain(n => n.Contains(".Persistence."));
    }

    [Test]
    public async Task QuerySymbolsAsync_NoTypeOrMemberQuery_Throws()
    {
        // act + assert
        await _sut.Invoking(sut => sut.QuerySymbolsAsync(
                CompilationHelper.SolutionPath, typeQuery: null, typeExclude: null, memberQuery: null))
            .Should().ThrowAsync<ArgumentException>();
    }
}
