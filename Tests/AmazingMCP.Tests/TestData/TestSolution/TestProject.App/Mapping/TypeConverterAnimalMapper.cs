using AutoMapper;
using TestProject.Core.Dtos;
using TestProject.Core.Models;

namespace TestProject.App.Mapping;

/// <summary>
/// Uses AutoMapper.ITypeConverter&lt;Animal, AnimalDto&gt; — a closed generic NuGet interface — as a dependency.
/// Used to verify that closed generic NuGet types are registered in Abstractions via open-generic fallback.
/// </summary>
public class TypeConverterAnimalMapper(ITypeConverter<Animal, AnimalDto> converter) : IEntityMapper<Animal, AnimalDto>
{
    public AnimalDto Map(Animal source) =>
        converter.Convert(source, null!, null!);

    public Animal MapBack(AnimalDto destination) => throw new NotImplementedException();
}
