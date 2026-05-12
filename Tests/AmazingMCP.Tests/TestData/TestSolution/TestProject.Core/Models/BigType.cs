namespace TestProject.Core.Models;

public class BigType
{
    public const int ConstA = 1;
    public const int ConstB = 2;

    public static int StaticField = 0;

    public int FieldA;
    public int FieldB;

    public int PropA { get; set; }
    public int PropB { get; set; }
    public int PropC { get; set; }
    public int PropD { get; set; }
    public int PropE { get; set; }

    public BigType() { }
    public BigType(int a) { }

    public void MethodA() { }
    public void MethodB() { }
    public void MethodC() { }
    public void MethodD() { }
    public void MethodE() { }
    public void MethodF() { }
    public void MethodG() { }
    public void MethodH() { }
    public int GetValue() => 0;
    public string GetName() => string.Empty;
    public bool IsValid() => true;
}
