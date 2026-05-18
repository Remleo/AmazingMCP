using AmazingMCP.Models.Design;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.Design;

public partial class DependencyMapServiceTests
{
    #region NuGet closed generic dependency — AutoMapper.ITypeConverter<Animal, AnimalDto>

    [Test]
    public async Task BuildMapAsync_NuGetClosedGenericDependency_AppearsInImplementationDependencies()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Mapping.TypeConverterAnimalMapper"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "AutoMapper.ITypeConverter<TestProject.Core.Models.Animal, TestProject.Core.Dtos.AnimalDto>");
    }

    [Test]
    public async Task BuildMapAsync_NuGetClosedGenericDependency_RegisteredInAbstractions()
    {
        var result = await Act();

        // The closed generic NuGet type must be registered in Abstractions via open-generic fallback
        result.Abstractions.Should().ContainKey(
            "AutoMapper.ITypeConverter<TestProject.Core.Models.Animal, TestProject.Core.Dtos.AnimalDto>");
    }

    [Test]
    public async Task BuildMapAsync_NuGetClosedGenericAbstraction_HasNullSourceFilePath()
    {
        var result = await Act();

        var abstraction = result.Abstractions[
            "AutoMapper.ITypeConverter<TestProject.Core.Models.Animal, TestProject.Core.Dtos.AnimalDto>"];
        abstraction.SourceFilePath.Should().BeNull();
        abstraction.IsInterface.Should().BeTrue();
    }

    [Test]
    public async Task BuildMapAsync_NuGetClosedGenericDependency_HasMethodCallUsage()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Mapping.TypeConverterAnimalMapper"];
        var dep = impl.Dependencies.First(d =>
            d.AbstractionFullName == "AutoMapper.ITypeConverter<TestProject.Core.Models.Animal, TestProject.Core.Dtos.AnimalDto>");
        dep.Usages.Should().Contain(u => u.MemberName == "Convert" && u.Kind == MemberUsageKind.MethodCall);
    }

    #endregion
}
