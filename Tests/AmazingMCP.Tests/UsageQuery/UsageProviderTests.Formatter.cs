using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class UsageProviderTests
{
    // ── Section line ranges ───────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_Match_SectionStartLineIsPositive()
    {
        // act
        var matches = await Act("TestProject.Core.Persistence.IAnimalRepository", predicate: "x.Kind == UsageKind.MethodCall");

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
        var matches = await Act("TestProject.Core.Persistence.IAnimalRepository", predicate: "x.Kind == UsageKind.MethodCall");

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
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

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
        var output = new UsageResultFormatter().Format(emptyMatches);

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

        var output = new UsageResultFormatter().Format(matches);

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
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

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
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

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
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

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
            "TestProject.Core.Persistence.IAnimalRepository",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

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
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

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
            "TestProject.Core.Persistence.IAnimalRepository",
            predicate: "x.Kind == UsageKind.MethodCall",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        var output = new UsageResultFormatter().Format(matches);

        // assert
        output.Should().Contain("// ...");
    }

    // ── Complex predicate ─────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_ComplexPredicate_OrCondition_FindsBothKinds()
    {
        // act
        var matches = await Act(
            "TestProject.Core.Models.Animal",
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
            "TestProject.App.Services.AnimalSnapshot",
            predicate: "x.Kind == UsageKind.PropertyWrite && x.PropertyName == \"Name\"",
            scanInclude: ["TestProject.App.Services.UsageQueryObjectInitFixture"]);

        // assert
        matches.Should().NotBeEmpty();
        matches.Should().Contain(m =>
            m.Entry.PropertyName == "Name" &&
            m.Scope.MethodName == "BuildSnapshot");
    }

    [Test]
    public async Task QueryAsync_FieldRead_InParamAttribute_SectionSpansMethodDeclaration()
    {
        // act — [DefaultValue(AnimalDefaults.MaxNameLength)] on a method parameter
        var matches = await Act(
            "TestProject.Core.Models.AnimalDefaults",
            predicate: "x.Kind == UsageKind.FieldRead && x.FieldName == \"MaxNameLength\"",
            scanInclude: ["TestProject.App.Services.ConstantInParamAttributeFixture"]);

        // assert — section must span the method declaration (attribute + method signature)
        var match = matches.Should().ContainSingle().Subject;
        (match.Scope.Section!.EndLine - match.Scope.Section.StartLine).Should().BeGreaterThan(0,
            "section should span the method declaration, not just the attribute line on the parameter");
    }
}