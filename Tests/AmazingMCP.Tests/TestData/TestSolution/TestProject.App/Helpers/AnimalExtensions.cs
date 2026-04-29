using TestProject.Core.Models;

namespace TestProject.App.Helpers;

/// <summary>
/// Extension methods for Animal — used to verify that get_symbol_details
/// correctly renders the 'this' parameter for extension methods.
/// </summary>
public static class AnimalExtensions
{
    public static string FormatLabel(this Animal animal, string prefix)
        => $"{prefix}: {animal.Name}";

    public static bool IsOfKind(this Animal animal, AnimalKind kind)
        => animal.Kind == kind;

    public static Animal WithName(this Animal animal, string newName)
    {
        animal.Name = newName;
        return animal;
    }
}
