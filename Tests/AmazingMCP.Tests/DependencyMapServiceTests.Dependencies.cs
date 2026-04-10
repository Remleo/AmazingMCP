using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Dependencies — detected via body scanning

    [Test]
    public async Task BuildMapAsync_InterfaceDependency_DetectedViaMethodCall()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.Core.Persistence.IAnimalRepository");
    }

    [Test]
    public async Task BuildMapAsync_IEnumerableDependency_ElementTypeDetectedViaMethodCall()
    {
        var result = await Act();

        // MultiValidatorService iterates _validators and calls v.Validate(animal)
        // So IAnimalValidator appears as a dependency
        var impl = result.Implementations["TestProject.App.Services.MultiValidatorService"];
        impl.Dependencies.Should().Contain(d =>
            d.AbstractionFullName == "TestProject.Core.Services.IAnimalValidator");
    }

    [Test]
    public async Task BuildMapAsync_NuGetDependency_DetectedViaMethodCall()
    {
        var result = await Act();

        var impl = result.Implementations["TestProject.App.Mapping.AutoMapperAnimalMapper"];
        // AutoMapper.IMapper.Map<T>() is declared on IMapperBase — Roslyn resolves ContainingType to IMapperBase
        impl.Dependencies.Should().Contain(d => d.AbstractionFullName.StartsWith("AutoMapper."));
    }

    #endregion
}
