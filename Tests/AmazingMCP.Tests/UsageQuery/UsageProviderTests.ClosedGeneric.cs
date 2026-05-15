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
    // ── MethodCall on closed generic receiver type ────────────────────────────

    [Test]
    public async Task QueryAsync_MethodCall_OnClosedGenericType_UsesReceiverType()
    {
        // arrange — _tracer.Trace(...) inside TraceOperation.
        // The method Trace is declared on IGenericTracer<T>, but the receiver is
        // IGenericTracer<UsageQueryTestFixture> — TypeName must reflect the closed generic.

        // act
        var matches = await Act(
            "TestProject.Core.Logging.IGenericTracer<TestProject.App.Services.UsageQueryTestFixture>",
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
            "TestProject.Core.Logging.IGenericTracer<TestProject.App.Services.UsageQueryTestFixture>",
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
            "TestProject.Core.Logging.IGenericTracer<TestProject.App.Services.UsageQueryTestFixture>",
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
            "TestProject.Core.Logging.IGenericTracer<TestProject.App.Services.UsageQueryTestFixture>",
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
            "TestProject.Core.Persistence.IAnimalRepository",
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
}