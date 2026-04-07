using AmazingMCP.Models;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Dependency member usages

    [Test]
    public async Task BuildMapAsync_MethodCallOnDependency_DetectedAsMethodCallUsage()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "FindById" && u.Kind == MemberUsageKind.MethodCall);
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Save" && u.Kind == MemberUsageKind.MethodCall);
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Notify" && u.Kind == MemberUsageKind.MethodCall);
    }

    [Test]
    public async Task BuildMapAsync_PropertyGetOnDependency_DetectedAsPropertyGetUsage()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    [Test]
    public async Task BuildMapAsync_UsagesFromBaseClass_IncludedInDerivedImplementation()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AdvancedAnimalService"];
        impl.DependencyMemberUsages.Should().Contain(u =>
            u.MemberName == "Count" && u.Kind == MemberUsageKind.PropertyGet);
    }

    #endregion
}
