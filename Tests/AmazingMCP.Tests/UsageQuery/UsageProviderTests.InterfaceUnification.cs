using AmazingMCP.Models.UsageQuery;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

/// <summary>
/// Tests that usages via a concrete type are also reported for all interfaces that type implicitly implements.
/// E.g. calling repo.FindById() where repo is SqlAnimalRepository should also appear as IAnimalRepository.FindById.
/// </summary>
public class UsageProviderTestsInterfaceUnification : UsageProviderTestsBase
{
    const string Fixture = "TestProject.Tests.ConcreteTypeCallFixture";
    const string Interface = "TestProject.Core.Persistence.IAnimalRepository";
    const string ConcreteType = "TestProject.Infrastructure.Persistence.SqlAnimalRepository";

    // ── MethodCall ────────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_MethodCall_ViaConcreteType_AlsoReportedForInterface()
    {
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"",
            scanInclude: [Fixture]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetById");
    }

    [Test]
    public async Task QueryAsync_MethodCall_ViaConcreteType_StillReportedForConcreteType()
    {
        var matches = await Act(
            ConcreteType,
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"",
            scanInclude: [Fixture]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "GetById");
    }

    // ── PropertyRead ──────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyRead_ViaConcreteType_AlsoReportedForInterface()
    {
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.PropertyRead && x.PropertyName == \"Count\"",
            scanInclude: [Fixture]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "IsFull");
    }

    // ── EventSubscribe ────────────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_EventSubscribe_ViaConcreteType_AlsoReportedForInterface()
    {
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.EventSubscribe && x.EventName == \"RepositoryChanged\"",
            scanInclude: [Fixture]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "Subscribe");
    }

    // ── EventCall (?.Invoke() inside the concrete type itself) ────────────────

    [Test]
    public async Task QueryAsync_EventCall_ViaConcreteType_AlsoReportedForInterface()
    {
        // SqlAnimalRepository.Save() calls RepositoryChanged?.Invoke(...)
        // — should appear when searching by IAnimalRepository
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.EventCall && x.EventName == \"RepositoryChanged\"",
            scanInclude: [ConcreteType]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "Save");
    }

    // ── PropertyWrite via explicit receiver ───────────────────────────────────

    [Test]
    public async Task QueryAsync_PropertyWrite_ViaConcreteType_AlsoReportedForInterface()
    {
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.PropertyWrite && x.PropertyName == \"DefaultKind\"",
            scanInclude: [Fixture]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "SetDefaultKind");
    }

    // ── Implicit-this: member used inside the implementing class without receiver

    [Test]
    public async Task QueryAsync_MethodCall_ImplicitThis_AlsoReportedForInterface()
    {
        // SqlAnimalRepository.ContainsId() calls FindById(id) without this.
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.MethodCall && x.MethodName == \"FindById\"",
            scanInclude: [ConcreteType]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "ContainsId");
    }

    [Test]
    public async Task QueryAsync_PropertyRead_ImplicitThis_AlsoReportedForInterface()
    {
        // SqlAnimalRepository.IsEmpty() reads Count without this.
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.PropertyRead && x.PropertyName == \"Count\"",
            scanInclude: [ConcreteType]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "IsEmpty");
    }

    [Test]
    public async Task QueryAsync_PropertyWrite_ImplicitThis_AlsoReportedForInterface()
    {
        // SqlAnimalRepository.ResetDefaultKind() writes DefaultKind without this.
        var matches = await Act(
            Interface,
            predicate: "x.Kind == UsageKind.PropertyWrite && x.PropertyName == \"DefaultKind\"",
            scanInclude: [ConcreteType]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "ResetDefaultKind");
    }
}
