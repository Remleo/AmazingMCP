using TestProject.Core.Models;
using TestProject.Core.Services;

namespace TestProject.App.Services;

public class MultiValidatorService
{
    readonly IEnumerable<IAnimalValidator> _validators;

    public MultiValidatorService(IEnumerable<IAnimalValidator> validators)
    {
        _validators = validators;
    }

    public bool ValidateAll(Animal animal)
    {
        foreach (var v in _validators)
        {
            if (!v.Validate(animal))
                return false;
        }
        return true;
    }
}
