namespace TestProject.Core.Models;

public readonly struct AnimalWeight(double kilograms)
{
    public double Kilograms { get; } = kilograms;

    public static AnimalWeight operator +(AnimalWeight a, AnimalWeight b) => new(a.Kilograms + b.Kilograms);
    public static bool operator >(AnimalWeight a, AnimalWeight b) => a.Kilograms > b.Kilograms;
    public static bool operator <(AnimalWeight a, AnimalWeight b) => a.Kilograms < b.Kilograms;

    public static implicit operator double(AnimalWeight w) => w.Kilograms;
    public static explicit operator AnimalWeight(double kg) => new(kg);
}
