using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class DependencyMapServiceTests
{
    #region Constructor dependencies

    [Test]
    public async Task BuildMapAsync_ConstructorDeps_InterfaceDependencyDetected()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.Core.Persistence.IAnimalRepository" &&
            !d.IsOptions && !d.IsEnumerable);
    }

    [Test]
    public async Task BuildMapAsync_ConstructorDeps_IOptionsUnwrapped()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.AnimalService"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.Core.Configuration.AnimalSettings" && d.IsOptions);
    }

    [Test]
    public async Task BuildMapAsync_ConstructorDeps_IEnumerableUnwrapped()
    {
        // act
        var result = await Act();

        // assert
        var impl = result.Implementations["TestProject.App.Services.MultiValidatorService"];
        impl.Dependencies.Should().Contain(d =>
            d.TypeFullName == "TestProject.Core.Services.IAnimalValidator" && d.IsEnumerable);
    }

    #endregion
}
