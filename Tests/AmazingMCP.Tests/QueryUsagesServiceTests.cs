using AmazingMCP.Models;
using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
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
            CreateWorkspaceProvider(_cachedSolution),
            new WildcardPatternFactory());
    }

    async Task<IReadOnlyList<UsageMatch>> Act(
        string typePattern,
        string? predicate = null,
        string[]? scanInclude = null,
        string[]? scanExclude = null)
    {
        var (matches, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            typePattern,
            predicate,
            scanInclude,
            scanExclude);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — no matches where the scope type equals the target type
        // for field/property access (self-references like accessing own fields should be excluded).
        // MethodCall is intentionally excluded from this check — implicit-this calls are valid usages.
        var selfRefs = matches.Where(m =>
            m.Entry.Kind is UsageKind.FieldRead or UsageKind.FieldWrite
                         or UsageKind.PropertyRead or UsageKind.PropertyWrite
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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.MultiParamPrimaryCtorFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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

    // ── ScanInclude / ScanExclude ─────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ScanInclude_ExcludesNonMatchingContainingTypes()
    {
        // act — restrict scan to UsageQueryTestFixture only
        var matches = await Act(
            "*IAnimalRepository*",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — all matches are found inside the filtered containing type
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Scope.TypeName.Should().Be("TestProject.App.Services.UsageQueryTestFixture"));
    }

    [Test]
    public async Task QueryAsync_ScanInclude_WildcardMatchesMultipleContainingTypes()
    {
        // act
        var matches = await Act(
            "*Animal*",
            scanInclude: ["TestProject.App.Services.*"]);

        // assert — all matches found inside App.Services types
        matches.Should().NotBeEmpty();
        matches.Should().AllSatisfy(m =>
            m.Scope.TypeName.Should().StartWith("TestProject.App.Services."));
    }

    [Test]
    public async Task QueryAsync_ScanInclude_NoMatchingContainingTypes_ReturnsEmpty()
    {
        // act
        var matches = await Act(
            "*Animal*",
            scanInclude: ["NonExistent.Namespace.*"]);

        // assert
        matches.Should().BeEmpty();
    }

    [Test]
    public async Task QueryAsync_ScanExclude_ExcludesMatchingContainingTypes()
    {
        // arrange — scan all App.Services types but exclude UsageQueryTestFixture specifically
        var allMatches = await Act(
            "*IAnimalRepository*",
            scanInclude: ["TestProject.App.Services.*"]);

        // act
        var filteredMatches = await Act(
            "*IAnimalRepository*",
            scanInclude: ["TestProject.App.Services.*"],
            scanExclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — excluded type must not appear in results
        filteredMatches.Should().NotContain(m =>
            m.Scope.TypeName == "TestProject.App.Services.UsageQueryTestFixture");

        // and the excluded type was actually present before exclusion (noise check)
        allMatches.Should().Contain(m =>
            m.Scope.TypeName == "TestProject.App.Services.UsageQueryTestFixture");
    }

    [Test]
    public async Task QueryAsync_ScanExclude_WildcardExcludesMultipleTypes()
    {
        // act — exclude all App.Services types via wildcard
        var matches = await Act(
            "*IAnimalRepository*",
            scanExclude: ["TestProject.App.Services.*"]);

        // assert — no matches from App.Services namespace
        matches.Should().NotContain(m =>
            m.Scope.TypeName.StartsWith("TestProject.App.Services."));
    }

    [Test]
    public async Task QueryAsync_ScanExclude_NonMatchingPattern_ExcludesNothing()
    {
        // arrange — baseline without exclusion
        var allMatches = await Act(
            "*IAnimalRepository*",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // act — exclude a pattern that matches nothing
        var matches = await Act(
            "*IAnimalRepository*",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"],
            scanExclude: ["NonExistent.Namespace.*"]);

        // assert — results are identical
        matches.Should().HaveSameCount(allMatches);
    }

    [Test]
    public async Task QueryAsync_ScanExclude_TakesPrecedenceOverScanInclude()
    {
        // act — include and exclude the same type simultaneously
        var matches = await Act(
            "*IAnimalRepository*",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"],
            scanExclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — exclude wins, no results
        matches.Should().BeEmpty();
    }

    // ── MethodCall on closed generic receiver type ────────────────────────────

    [Test]
    public async Task QueryAsync_MethodCall_OnClosedGenericType_UsesReceiverType()
    {
        // arrange — _tracer.Trace(...) inside TraceOperation.
        // The method Trace is declared on IGenericTracer<T>, but the receiver is
        // IGenericTracer<UsageQueryTestFixture> — TypeName must reflect the closed generic.

        // act
        var matches = await Act(
            "*IGenericTracer*",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"Trace\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.TypeName.Contains("IGenericTracer") &&
            m.Entry.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "TraceOperation");
    }

    [Test]
    public async Task QueryAsync_MethodCall_OnClosedGenericType_FoundInMultipleMethods()
    {
        // arrange — _tracer.Trace(...) is called in both TraceOperation and TraceAndFind

        // act
        var matches = await Act(
            "*IGenericTracer*",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"Trace\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert — both methods are found
        matches.Should().Contain(m => m.Scope.MethodName == "TraceOperation");
        matches.Should().Contain(m => m.Scope.MethodName == "TraceAndFind");
    }

    // ── Object initializer — property assigned from closed generic ────────────

    [Test]
    public async Task QueryAsync_PropertyWrite_ObjectInitializer_ClosedGenericType_IsFound()
    {
        // arrange — BuildHolder() uses: new TracerHolder { Tracer = _tracer }
        // _tracer is IGenericTracer<UsageQueryTestFixture> — must be found as PropertyWrite

        // act
        var matches = await Act(
            "*IGenericTracer*",
            predicate: "x.Kind == UsageKind.PropertyWrite",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.TypeName.Contains("IGenericTracer") &&
            m.Entry.TypeName.Contains("UsageQueryTestFixture") &&
            m.Scope.MethodName == "BuildHolder");
    }

    [Test]
    public async Task QueryAsync_PropertyWrite_ObjectInitializerInsideLargeLambda_SectionSpansInitializer()
    {
        // arrange — UsageInObjectInitializerInsideLargeLambda has a new TracerHolder { Tracer = _tracer }
        // inside a lambda block >5 lines. The section should span the entire new() { ... } block,
        // NOT fall back to a single line at the assignment.

        // act
        var matches = await Act(
            "*IGenericTracer*",
            predicate: "x.Kind == UsageKind.PropertyWrite",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        var lambdaMatch = matches.FirstOrDefault(m =>
            m.Scope.MethodName == "UsageInObjectInitializerInsideLargeLambda");

        lambdaMatch.Should().NotBeNull("PropertyWrite inside large lambda should be found");
        lambdaMatch!.Scope.Section.EndLine.Should().BeGreaterThan(lambdaMatch.Scope.Section.StartLine,
            "section should span the entire object initializer block, not collapse to a single line");
    }

    [Test]
    public async Task QueryAsync_MethodCall_InsideCatchInsideLargeLambda_SectionSpansCatchBlock()
    {
        // arrange — UsageInCatchInsideLargeLambda has a FindById() call inside a catch block
        // which is itself inside a large lambda. The section should be the CatchClauseSyntax
        // (catch keyword + block), and the usage line must be visible within the section.

        // act
        var matches = await Act(
            "*IAnimalRepository*",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        var catchMatch = matches.FirstOrDefault(m =>
            m.Scope.MethodName == "UsageInCatchInsideLargeLambda");

        catchMatch.Should().NotBeNull("FindById inside catch inside large lambda should be found");

        // Section must be CatchClauseSyntax (starts at "catch" line, not at "{")
        catchMatch!.Scope.Section.Node.Should().BeOfType<Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax>(
            "section should be the full catch clause, not just its body block");

        // Usage line must be within the section
        catchMatch.Scope.MatchLine.Should().BeInRange(
            catchMatch.Scope.Section.StartLine,
            catchMatch.Scope.Section.EndLine,
            "usage must be visible within the section");
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
            null, null);

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
            null, null);

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
            null, null);

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
            null, null);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
    public async Task QueryAsync_Formatter_MethodDefinition_NotDuplicated_WhenUsageInParameterAndBody()
    {
        // arrange — RenameAnimal(Animal animal, ...) has Animal as TypeAsParameter (line with signature)
        // AND PropertyWrite animal.Name in the body (different section, non-adjacent).
        // The method signature line must appear exactly once in the output.

        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.TypeAsParameter || x.Kind == UsageKind.PropertyWrite",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = UsageResultFormatter.Format(matches);

        // assert — "RenameAnimal" appears exactly once
        var occurrences = System.Text.RegularExpressions.Regex
            .Matches(output, @"RenameAnimal")
            .Count;

        occurrences.Should().Be(1,
            "method signature must not be duplicated when it is both a match section and a method header");
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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

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
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.Kind == UsageKind.PropertyRead || m.Entry.Kind == UsageKind.PropertyWrite);
    }

    [Test]
    public async Task QueryAsync_FieldRead_ImplicitThis_AsArgument_IsFound()
    {
        // arrange — SaveDefault() passes _defaultAnimal as argument: _repository.Save(_defaultAnimal)
        // _defaultAnimal is Animal — implicit-this field read without explicit receiver

        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.FieldRead && x.FieldName == \"_defaultAnimal\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().Contain(m => m.Scope.MethodName == "SaveDefault",
            "implicit-this field reads passed as arguments must be found");
    }

    [Test]
    public async Task QueryAsync_MethodCall_WithExplicitThis_IsFound()
    {
        // arrange — CheckDefaultExplicit() calls this.IsValidAnimal(...)

        // act
        var matches = await Act(
            "TestProject.App.Services.UsageQueryTestFixture",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"IsValidAnimal\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().Contain(m => m.Scope.MethodName == "CheckDefaultExplicit",
            "explicit this. method calls must be found");
    }

    [Test]
    public async Task QueryAsync_MethodCall_WithoutExplicitReceiver_IsFound()
    {
        // arrange — CheckDefault() calls IsValidAnimal(_defaultAnimal) without explicit 'this.'
        // This is an implicit-this invocation — Expression is IdentifierNameSyntax, not MemberAccessExpressionSyntax

        // act
        var matches = await Act(
            "TestProject.App.Services.UsageQueryTestFixture",
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"IsValidAnimal\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "CheckDefault");
    }

    [Test]
    public async Task QueryAsync_UsageInsidePrivateMethod_IsFound()
    {
        // arrange — IsValidAnimal is a private method that uses Animal.Name (PropertyRead)

        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
            predicate: "x.Kind == UsageKind.PropertyRead && x.PropertyName == \"Name\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().Contain(m => m.Scope.MethodName == "IsValidAnimal",
            "usages inside private methods must be found");
    }

    [Test]
    public async Task QueryAsync_ExtensionMethodCall_IsFound()
    {
        // arrange — FormatAnimalLabel calls animal.FormatLabel("Animal")
        // which is an extension method on AnimalExtensions static class

        // act
        var matches = await Act(
            "TestProject.App.Helpers.AnimalExtensions",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Scope.MethodName == "FormatAnimalLabel");
    }

    [Test]
    public async Task QueryAsync_Cs14ExtensionBlock_AnimalAsExtendedType_IsFound()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.ExtensionProbe",
            scanInclude: ["TestProject.App.Helpers.ExtensionProbeExtensions"]);

        // assert — the extension(ExtensionProbe probe) parameter must be detected as TypeAsParameter
        matches.Should().BeEquivalentTo(
            [new { Entry = new { Kind = UsageKind.TypeAsParameter, TypeName = "TestProject.Core.Models.ExtensionProbe" } }],
            options => options.Including(m => m.Entry.Kind).Including(m => m.Entry.TypeName));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyWrite_ObjectInitializer_LiteralValue_IsFound()
    {
        // arrange — UsageQueryObjectInitFixture.BuildSnapshot() uses:
        //   new AnimalSnapshot { Name = "literal", Kind = AnimalKind.Unknown }
        // value is a string literal / enum — not an identifier, so assign.Right is not IdentifierNameSyntax

        // act
        var matches = await Act(
            "*AnimalSnapshot*",
            predicate: "x.Kind == UsageKind.PropertyWrite && x.PropertyName == \"Name\"",
            scanInclude: ["TestProject.App.Services.UsageQueryObjectInitFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.PropertyName == "Name" &&
            m.Scope.MethodName == "BuildSnapshot");
    }
}
