using AmazingMCP.Models.Design;
using AmazingMCP.Services.Design;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.Design;

public partial class DependencyMapServiceTests
{
    #region Dependency member usages — direct

    [Test]
    public async Task BuildMapAsync_MethodCallOnDependency_DetectedAsMethodCallUsage()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        var allUsages = impl.Dependencies.SelectMany(d => d.Usages).ToList();
        allUsages.Should().Contain(u => u.MemberName == "FindById" && u.Kind == MemberUsageKind.MethodCall);
        allUsages.Should().Contain(u => u.MemberName == "Save" && u.Kind == MemberUsageKind.MethodCall);
        allUsages.Should().Contain(u => u.MemberName == "Notify" && u.Kind == MemberUsageKind.MethodCall);
    }

    [Test]
    public async Task BuildMapAsync_PropertyGetOnDependency_DetectedAsPropertyGetUsage()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        var allUsages = impl.Dependencies.SelectMany(d => d.Usages).ToList();
        allUsages.Should().Contain(u => u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    #endregion

    #region Dependency member usages — base class (direct, not aggregated)

    [Test]
    public async Task BuildMapAsync_BaseClass_HasOwnDirectUsages()
    {
        var result = await Act();

        // AnimalServiceBase directly uses IAnimalRepository.Count
        var baseImpl = result.Implementations["TestProject.App.Services.AnimalServiceBase"];
        var allUsages = baseImpl.Dependencies.SelectMany(d => d.Usages).ToList();
        allUsages.Should().Contain(u => u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    [Test]
    public async Task BuildMapAsync_DerivedClass_DoesNotContainBaseClassUsagesDirectly()
    {
        var result = await Act();

        // AdvancedAnimalService's own body does NOT call Count — that's in AnimalServiceBase
        var derived = result.Implementations["TestProject.App.Services.AdvancedAnimalService"];
        var directUsages = derived.Dependencies.SelectMany(d => d.Usages).ToList();
        // Direct body of AdvancedAnimalService calls FindById, FindByKind, Save, Notify
        // Count is NOT in its direct body
        directUsages.Should().NotContain(u => u.MemberName == "Count");
    }

    #endregion

    #region Aggregated usages via DependencyAggregator

    [Test]
    public async Task BuildMapAsync_AggregatedUsages_IncludeBaseClassUsages()
    {
        var result = await Act();
        var aggregator = new DependencyAggregator();

        var allUsages = aggregator.GetAllUsages(
            "TestProject.App.Services.AdvancedAnimalService", result);

        allUsages.SelectMany(u => u.Usages)
            .Should().Contain(u => u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    #endregion
}
