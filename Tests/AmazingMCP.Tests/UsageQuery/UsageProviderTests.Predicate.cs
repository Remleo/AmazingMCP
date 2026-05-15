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
    // ── Predicate safety ──────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_UnsafePredicate_ObjectCreation_ReturnsError()
    {
        // act
        var (_, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            "TestProject.Core.Models.Animal",
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
            "TestProject.Core.Models.Animal",
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
            "TestProject.Core.Persistence.IAnimalRepository",
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
            "TestProject.Core.Persistence.IAnimalRepository",
            "x.Kind == UsageKind.MethodCall && (x.ArgumentTypes == null || !x.ArgumentTypes.Any())",
            null, null);

        // assert
        error.Should().BeNull();
    }
}