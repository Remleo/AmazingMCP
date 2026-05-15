using AutoMapper;
using TestProject.Core.Dtos;
using TestProject.Core.Models;

namespace TestProject.App.Mapping;

public class AnimalMappingProfile : Profile
{
    public AnimalMappingProfile()
    {
        CreateMap<Animal, AnimalDto>();
    }
}
