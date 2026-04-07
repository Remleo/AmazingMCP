using TestProject.Core.Models;

namespace TestProject.Core.Services;

public interface IAnimalValidator
{
    bool Validate(Animal animal);
}
