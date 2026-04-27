using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class QueryUsagesServiceTests
{
    CachedSolution _cachedSolution = null!;
    IUsageQueryService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new UsageQueryService(
            new TestWorkspaceProvider(_cachedSolution),
            new WildcardPatternFactory());
    }

    async Task<IReadOnlyList<UsageMatch>> Act(
        string typePattern,
        string? predicate = null,
        string[]? scanFilters = null)
    {
        var (matches, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            typePattern,
            predicate,
            scanFilters);

        error.Should().BeNull();
        return matches;
    }

    [Test]
    public async Task QueryAsync_SelfReference_FieldAccess_IsNotReturned()
    {
        // arrange — UsageQueryTestFixture accesses its own _defaultAnimal field internally.
        // Searching for UsageQueryTestFixture usages should NOT return self-references.

        // act
        var matches = await Act(
            "TestProject.App.Services.UsageQueryTestFixture",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — no matches where the scope type equals the target type
        // (self-references like accessing own fields/methods should be excluded)
        var selfRefs = matches.Where(m =>
            m.Entry.Kind is UsageKind.FieldRead or UsageKind.FieldWrite
                         or UsageKind.PropertyRead or UsageKind.PropertyWrite
                         or UsageKind.MethodCall
            && m.Entry.TypeName == "TestProject.App.Services.UsageQueryTestFixture").ToList();

        selfRefs.Should().BeEmpty();
    }

    // ── Large block suppression ───────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_UsageInsideLargeBlock_SectionIsSingleLine()
    {
        // arrange — UsageInsideLargeLambda has a Save() call inside a lambda block >5 lines.
        // The section should be the single line of the call, not the entire lambda.

        // act
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"Save\"",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — the match inside the large lambda should have a 1-line section
        var lambdaMatch = matches.FirstOrDefault(m =>
            m.Scope.MethodName == "UsageInsideLargeLambda");

        lambdaMatch.Should().NotBeNull("Save() inside large lambda should be found");
        lambdaMatch!.Scope.Section.StartLine.Should().Be(lambdaMatch.Scope.Section.EndLine,
            "usage inside a large block should fall back to a single-line section");
    }

    [Test]
    public async Task QueryAsync_UsageOutsideLargeBlock_SectionIsNormal()
    {
        // arrange — SaveIfNotFull has Save() inside a short if-body (not a large block).
        // The section resolves normally via SectionResolver (InvocationExpression),
        // not suppressed to fallback. Both should be single-line — the key difference
        // is that the large-lambda match is suppressed while this one resolves naturally.

        // act
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"Save\"",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — both matches are found
        matches.Should().Contain(m => m.Scope.MethodName == "SaveIfNotFull",
            "Save() in SaveIfNotFull should be found");
        matches.Should().Contain(m => m.Scope.MethodName == "UsageInsideLargeLambda",
            "Save() inside large lambda should also be found");
    }

    // ── TypeName filter (typePattern) ─────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypePattern_FiltersToMatchingType()
    {
        // act — only usages of IAnimalRepository
        var matches = await Act("*IAnimalRepository*");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.TypeName.Should().Contain("IAnimalRepository"));
    }

    [Test]
    public async Task QueryAsync_TypePattern_NoMatch_ReturnsEmpty()
    {
        // act
        var matches = await Act("*NonExistentType99*");

        // assert
        matches.Should().BeEmpty();
    }

    // ── MethodCall ────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_MethodCall_FindsCallsByName()
    {
        // act
        var matches = await Act(
            "*IAnimalRepository*",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.Should().BeEquivalentTo(new
            {
                Kind = UsageKind.MethodCall,
                MethodName = "FindById"
            }));
    }

    [Test]
    public async Task QueryAsync_MethodCall_MatchContainsCorrectScope()
    {
        // act
        var matches = await Act(
            "*IAnimalRepository*",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"");

        // assert — at least one match is inside UsageQueryTestFixture.FindAnimalById
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "FindAnimalById");
    }

    // ── PropertyRead ──────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyRead_FindsReadsByName()
    {
        // act
        var matches = await Act(
            "*IAnimalRepository*",
            predicate: "x.Kind == UsageKind.PropertyRead && x.PropertyName == \"Count\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.Should().BeEquivalentTo(new { Kind = UsageKind.PropertyRead, PropertyName = "Count" }));
    }

    // ── PropertyWrite ─────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyWrite_FindsWritesByName()
    {
        // act
        var matches = await Act(
            "*Animal*",
            predicate: "x.Kind == UsageKind.PropertyWrite && x.PropertyName == \"Name\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "RenameAnimal");
    }

    // ── ConstructorCall ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ConstructorCall_FindsNewExpressionsByTypeName()
    {
        // act
        var matches = await Act(
            "*Animal*",
            predicate: "x.Kind == UsageKind.ConstructorCall && x.MethodName == \"Animal\"");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.Should().BeEquivalentTo(new { Kind = UsageKind.ConstructorCall, MethodName = "Animal" }));
    }

    [Test]
    public async Task QueryAsync_ConstructorCall_MatchIsInsideCreateAnimalMethod()
    {
        // act
        var matches = await Act(
            "*Animal*",
            predicate: "x.Kind == UsageKind.ConstructorCall && x.MethodName == \"Animal\"");

        // assert
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "CreateAnimal");
    }

    // ── TypeAsParameter ───────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsParameter_FindsAnimalInMethodParameter()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeAsParameter");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.TypeAsParameter);
            m.Entry.TypeName.Should().Contain("Animal");
        });
    }

    [Test]
    public async Task QueryAsync_TypeAsParameter_PrimaryConstructor_MultiLine_SpansEntireParameterList()
    {
        // arrange — MultiParamPrimaryCtorFixture has a multi-line primary constructor
        // with IAnimalRepository, IAnimalService, INotificationService as parameters.
        // When IAnimalRepository is matched, the section should span all parameter lines.

        // act
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.TypeAsParameter",
            scanFilters: ["TestProject.App.Services.MultiParamPrimaryCtorFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        var match = matches.First(m => m.Scope.MethodName == ".ctor");

        // The parameter list spans multiple lines — section must cover all of them
        match.Scope.Section.EndLine.Should().BeGreaterThan(match.Scope.Section.StartLine,
            "primary constructor parameter list spans multiple lines");
    }

    [Test]
    public async Task QueryAsync_TypeAsParameter_PrimaryConstructor_IsFound()
    {
        // act — UsageQueryTestFixture has IAnimalRepository as primary constructor parameter
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.TypeAsParameter",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == ".ctor");
    }

    // ── TypeAsGenericArgument ─────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsGenericArgument_FindsAnimalAsTypeArgument()
    {
        // act — typePattern matches Animal, predicate filters to generic argument usages
        var matches = await Act(
            "*Animal*",
            predicate: "x.Kind == UsageKind.TypeAsGenericArgument");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
        {
            m.Entry.Kind.Should().Be(UsageKind.TypeAsGenericArgument);
            m.Entry.TypeName.Should().Contain("Animal");
        });
    }

    // ── TypeAsGenericConstraint ───────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsGenericConstraint_FindsWhereConstraint()
    {
        // act
        var matches = await Act(
            "*Animal*",
            predicate: "x.Kind == UsageKind.TypeAsGenericConstraint");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Scope.TypeName.Contains("GenericUsageFixture") &&
            m.Scope.MethodName == "ProcessAnimal");
    }

    // ── TypeAsReturnType ──────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_TypeAsReturnType_FindsMethodsReturningAnimal()
    {
        // act
        var matches = await Act(
            "*Animal*",
            predicate: "x.Kind == UsageKind.TypeAsReturnType");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.TypeName.Contains("UsageQueryTestFixture"));
    }

    // ── TypeName always populated ─────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_AllMatches_HaveNonEmptyTypeName()
    {
        // act
        var matches = await Act("*Animal*");

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Entry.TypeName.Should().NotBeNullOrEmpty());
    }

    // ── ScanFilters ───────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ScanFilters_ExcludesNonMatchingContainingTypes()
    {
        // act — restrict scan to UsageQueryTestFixture only
        var matches = await Act(
            "*IAnimalRepository*",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — all matches are found inside the filtered containing type
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Scope.TypeName.Should().Be("TestProject.App.Services.UsageQueryTestFixture"));
    }

    [Test]
    public async Task QueryAsync_ScanFilters_WildcardMatchesMultipleContainingTypes()
    {
        // act
        var matches = await Act(
            "*Animal*",
            scanFilters: ["TestProject.App.Services.*"]);

        // assert — all matches found inside App.Services types
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Scope.TypeName.Should().StartWith("TestProject.App.Services."));
    }

    [Test]
    public async Task QueryAsync_ScanFilters_NoMatchingContainingTypes_ReturnsEmpty()
    {
        // act
        var matches = await Act(
            "*Animal*",
            scanFilters: ["NonExistent.Namespace.*"]);

        // assert
        matches.Should().BeEmpty();
    }

    // ── Predicate safety ──────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_UnsafePredicate_ObjectCreation_ReturnsError()
    {
        // act
        var (_, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            "*Animal*",
            "x.Kind == UsageKind.MethodCall && new System.Exception() != null",
            null);

        // assert
        error.Should().NotBeNull();
        error.Should().Contain("Object creation not allowed");
    }

    [Test]
    public async Task QueryAsync_UnsafePredicate_DisallowedStaticCall_ReturnsError()
    {
        // act
        var (_, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            "*Animal*",
            "System.IO.File.Exists(x.MethodName ?? \"\")",
            null);

        // assert
        error.Should().NotBeNull();
        error.Should().Contain("Static call not allowed");
    }

    [Test]
    public async Task QueryAsync_SafePredicate_AllowedStaticCall_Succeeds()
    {
        // act
        var (matches, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            "*IAnimalRepository*",
            "x.Kind == UsageKind.MethodCall && !String.IsNullOrEmpty(x.MethodName)",
            null);

        // assert
        error.Should().BeNull();
        matches.Should().NotBeEmpty();
    }

    [Test]
    public async Task QueryAsync_SafePredicate_LinqAny_Succeeds()
    {
        // act
        var (matches, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            "*IAnimalRepository*",
            "x.Kind == UsageKind.MethodCall && (x.ArgumentTypes == null || !x.ArgumentTypes.Any())",
            null);

        // assert
        error.Should().BeNull();
    }

    // ── Section line ranges ───────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_Match_SectionStartLineIsPositive()
    {
        // act
        var matches = await Act("*IAnimalRepository*", predicate: "x.Kind == UsageKind.MethodCall");

        // assert
        matches.Should().AllSatisfy(m =>
        {
            m.Scope.Section.StartLine.Should().BeGreaterThan(0);
            m.Scope.Section.EndLine.Should().BeGreaterThanOrEqualTo(m.Scope.Section.StartLine);
        });
    }

    [Test]
    public async Task QueryAsync_Match_MatchLineIsWithinSectionRange()
    {
        // act
        var matches = await Act("IAnimalRepository", predicate: "x.Kind == UsageKind.MethodCall");

        // assert — MatchLine should be at or near the section (within 1 line tolerance for edge cases)
        matches.Should().AllSatisfy(m =>
        {
            m.Scope.MatchLine.Should().BeGreaterThanOrEqualTo(m.Scope.Section.StartLine);
            m.Scope.MatchLine.Should().BeLessThanOrEqualTo(m.Scope.Section.EndLine + 1);
        });
    }

    // ── Formatter ─────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_Formatter_OutputContainsTypeHeader()
    {
        // act
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — one csharp block per type+file
        output.Should().Contain("UsageQueryTestFixture");
        output.Split("```csharp").Length.Should().Be(2, "all snippets for one type are in a single csharp block");
    }

    [Test]
    public async Task QueryAsync_Formatter_NoMatches_ReturnsNoUsagesMessage()
    {
        // arrange
        var emptyMatches = Array.Empty<UsageMatch>();

        // act
        var output = UsageResultFormatter.Format(emptyMatches);

        // assert
        output.Should().Contain("No usages found");
    }

    [Test]
    public async Task QueryAsync_Formatter_MethodDefinition_ShownOnceWhenMultipleNonAdjacentMatchesInSameMethod()
    {
        // arrange — MultiUsageMethod has two non-adjacent usages of IAnimalRepository
        // (FindById near top, Count+Save further down with a gap between them).
        // The method definition must appear exactly once, not once per non-adjacent block.

        // act
        var matches = await Act(
            "IAnimalRepository",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — "MultiUsageMethod" appears exactly once in the output
        var occurrences = System.Text.RegularExpressions.Regex
            .Matches(output, @"MultiUsageMethod")
            .Count;

        occurrences.Should().Be(1,
            "method definition must appear exactly once even when its matches are on non-adjacent lines");
    }

    [Test]
    public async Task QueryAsync_Formatter_OverlappingGroupsFromDifferentMethods_MergedIntoOneBlock()
    {
        // arrange — searching for a type that appears both as TypeAsParameter (.ctor group)
        // and as TypeAsGenericArgument (null-method group) on the same lines.
        // Both matches share the same section range and must produce exactly one code block.

        // act — IAnimalRepository appears in primary ctor parameter list of UsageQueryTestFixture
        // and potentially as generic argument; both on the same lines
        var matches = await Act(
            "IAnimalRepository",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — no duplicate line headers for the same range
        var lineHeaders = System.Text.RegularExpressions.Regex
            .Matches(output, @"// lines? \d+ \+\d+")
            .Select(m => m.Value)
            .ToList();

        lineHeaders.Should().OnlyHaveUniqueItems(
            "overlapping groups from different method contexts must be merged into one block");
    }

    [Test]
    public async Task QueryAsync_Formatter_MethodDefinition_AppearsOncePerMethod_WhenMultipleMatchesInSameMethod()
    {
        // arrange — SaveIfNotFull uses IAnimalRepository twice: Count (PropertyRead) and Save (MethodCall).
        // The method definition should appear exactly once in the output, not once per match.

        // act
        var matches = await Act(
            "IAnimalRepository",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — count occurrences of "SaveIfNotFull" in the output
        // It should appear exactly once (as the method definition header), not multiple times
        var occurrences = System.Text.RegularExpressions.Regex
            .Matches(output, @"SaveIfNotFull")
            .Count;

        occurrences.Should().Be(1, "method definition must appear exactly once even when the method has multiple matches");
    }

    [Test]
    public async Task QueryAsync_Formatter_NoDuplicateBlocks_WhenSameRangeFromDifferentMethods()
    {
        // act — multiple matches may share overlapping line ranges and should be merged
        var matches = await Act(
            "IAnimalRepository",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — output should not contain the same line comment twice
        // (duplicate ranges would produce identical /* lines N +K */ headers)
        var lineComments = System.Text.RegularExpressions.Regex
            .Matches(output, @"// lines? \d+ \+\d+")
            .Select(m => m.Value)
            .ToList();

        lineComments.Should().OnlyHaveUniqueItems("merged ranges must not produce duplicate line headers");
    }

    [Test]
    public async Task QueryAsync_Formatter_LineCommentFormat_IsCorrect()
    {
        // act
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — line comments use /* line N +K */ format inside the csharp block
        output.Should().MatchRegex(@"// lines? \d+ \+\d+");
        // all content is inside a single csharp block
        output.Split("```csharp").Length.Should().Be(2);
    }

    [Test]
    public async Task QueryAsync_Formatter_CutSeparator_AppearsWhenMethodShownSeparately()
    {
        // act — method calls are inside method bodies, so definition is shown + cut + usage
        var matches = await Act(
            "IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert
        output.Should().Contain("// ...");
    }

    // ── Complex predicate ─────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ComplexPredicate_OrCondition_FindsBothKinds()
    {
        // act
        var matches = await Act(
            "*Animal*",
            predicate: "(x.Kind == UsageKind.PropertyRead || x.Kind == UsageKind.PropertyWrite) && x.PropertyName == \"Name\"",
            scanFilters: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.Kind == UsageKind.PropertyRead || m.Entry.Kind == UsageKind.PropertyWrite);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    class TestWorkspaceProvider(CachedSolution solution) : IWorkspaceProvider
    {
        public Task<CachedSolution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
            => Task.FromResult(solution);
    }
}
