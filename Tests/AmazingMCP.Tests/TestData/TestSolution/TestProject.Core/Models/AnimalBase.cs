namespace TestProject.Core.Models;

public abstract class AnimalBase
{
    public abstract string GetName();
    public abstract int GetScore(int input);
    protected abstract string FormatDescription();
    public virtual string GetSummary() => GetName();
    public abstract int AbstractProp { get; set; }
    public virtual string VirtualPropOnBase { get; set; } = "";
}

public class ConcreteAnimal : AnimalBase
{
    public override string GetName() => "ConcreteAnimal";
    public override int GetScore(int input) => input * 2;
    protected override string FormatDescription() => "concrete";
    public sealed override string GetSummary() => "sealed-summary";
    public override int AbstractProp { get; set; }
    public override string VirtualPropOnBase { get; set; } = "overridden";
}
