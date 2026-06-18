using AmazingMCP.Models.UsageQuery;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

public class UsageProviderTestsEvents : UsageProviderTestsBase
{
    const string Repository = "TestProject.Core.Persistence.IAnimalRepository";
    const string EventCallFixtureType = "TestProject.App.Services.EventCallFixture";

    // ── EventSubscribe (+=) ───────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_EventSubscribe_IsFound()
    {
        var matches = await Act(
            Repository,
            predicate: "x.Kind == UsageKind.EventSubscribe",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        matches.Should().NotBeEmpty();
    }

    [Test]
    public async Task QueryAsync_EventSubscribe_HasCorrectEventName()
    {
        var matches = await Act(
            Repository,
            predicate: "x.Kind == UsageKind.EventSubscribe && x.EventName == \"RepositoryChanged\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "SubscribeToRepository");
    }

    // ── EventUnsubscribe (-=) ─────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_EventUnsubscribe_IsFound()
    {
        var matches = await Act(
            Repository,
            predicate: "x.Kind == UsageKind.EventUnsubscribe",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        matches.Should().NotBeEmpty();
    }

    [Test]
    public async Task QueryAsync_EventUnsubscribe_HasCorrectEventName()
    {
        var matches = await Act(
            Repository,
            predicate: "x.Kind == UsageKind.EventUnsubscribe && x.EventName == \"RepositoryChanged\"",
            scanInclude: ["TestProject.App.Services.UsageQueryTestFixture"]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "UnsubscribeFromRepository");
    }

    // ── EventCall (?.Invoke()) ────────────────────────────────────────────────

    [Test]
    public async Task QueryAsync_EventCall_IsFound()
    {
        var matches = await Act(
            EventCallFixtureType,
            predicate: "x.Kind == UsageKind.EventCall",
            scanInclude: ["TestProject.App.Services.EventCallFixture"]);

        matches.Should().NotBeEmpty();
    }

    [Test]
    public async Task QueryAsync_EventCall_HasCorrectEventName()
    {
        var matches = await Act(
            EventCallFixtureType,
            predicate: "x.Kind == UsageKind.EventCall && x.EventName == \"StatusChanged\"",
            scanInclude: ["TestProject.App.Services.EventCallFixture"]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "NotifyStatusChanged");
    }

    [Test]
    public async Task QueryAsync_EventCall_DirectInvocation_IsFound()
    {
        var matches = await Act(
            EventCallFixtureType,
            predicate: "x.Kind == UsageKind.EventCall && x.EventName == \"StatusChanged\"",
            scanInclude: ["TestProject.App.Services.EventCallFixture"]);

        matches.Should().NotBeEmpty();
        matches.Should().Contain(m => m.Scope.MethodName == "NotifyStatusChangedDirect");
    }
}
