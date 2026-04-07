using AutoMapper;
using TestProject.Core.Dtos;
using TestProject.Core.Models;

namespace TestProject.App.Mapping;

/// <summary>
/// IEntityMapper implementation that delegates to AutoMapper's IMapper (NuGet).
/// Used to verify that NuGet-sourced dependencies are tracked but excluded from ProjectDesign groups.
/// </summary>
public class AutoMapperAnimalMapper : IEntityMapper<Animal, AnimalDto>
{
    readonly IMapper _mapper;

    public AutoMapperAnimalMapper(IMapper mapper)
    {
        _mapper = mapper;
    }

    public AnimalDto Map(Animal source) => _mapper.Map<AnimalDto>(source);

    public Animal MapBack(AnimalDto destination) => _mapper.Map<Animal>(destination);
}
