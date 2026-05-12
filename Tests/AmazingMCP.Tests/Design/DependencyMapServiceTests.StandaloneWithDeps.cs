using AmazingMCP.Models;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

/// <summary>
/// Tests for standalone classes (no interface) that have dependencies.
/// Covers the bug where EnsureAbstraction pre-registers a standalone class
/// with empty Implementations before it is scanned, causing its Implementation
/// entry to be missing from the map.
/// </summary>
public partial class DependencyMapServiceTests
{
    #region Standalone class with dependencies — consumed by another class

    [Test]
    public async Task BuildMapAsync_StandaloneClassWithDeps_AppearsInAbstractions()
    {
        var result = await Act();

        // AnimalFormatter has no interface but has dependencies (IAnimalRepository)
        // and is used by AnimalFormatterConsumer — must appear as abstraction
        result.Abstractions.Should().ContainKey("TestProject.App.Helpers.AnimalFormatter");
    }

    [Test]
    public async Task BuildMapAsync_StandaloneClassWithDeps_AppearsInImplementations()
    {
        var result = await Act();

        // The bug: AnimalFormatterConsumer triggers EnsureAbstraction for AnimalFormatter
        // before AnimalFormatter itself is scanned. Without the fix, AnimalFormatter
        // would be registered with empty Implementations and never get its own entry.
        result.Implementations.Should().ContainKey("TestProject.App.Helpers.AnimalFormatter");
    }

    [Test]
    public async Task BuildMapAsync_StandaloneClassWithDeps_HasCorrectDependencies()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Helpers.AnimalFormatter"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.Core.Persistence.IAnimalRepository");
    }

    [Test]
    public async Task BuildMapAsync_StandaloneClassWithDeps_AbstractionHasItselfAsImplementation()
    {
        var result = await Act();

        var abstraction = result.Abstractions["TestProject.App.Helpers.AnimalFormatter"];
        abstraction.Implementations.Should().Contain("TestProject.App.Helpers.AnimalFormatter");
    }

    [Test]
    public async Task BuildMapAsync_StandaloneClassConsumer_HasStandaloneAsDependency()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Helpers.AnimalFormatterConsumer"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.App.Helpers.AnimalFormatter");
    }

    #endregion
}
