namespace TestProject.Core.Models;

public class AnimalDefaults
{
    public string DisplayLabel = "default";
    public readonly int MaxRetries = 3;
    internal string InternalTag = "tag";
    protected readonly int ProtectedRetryLimit = 5;
    protected internal string ProtectedInternalTag = "pi-tag";
    private protected int PrivateProtectedScore = 42;

    public AnimalDefaults() { }
    public AnimalDefaults(string displayLabel) { DisplayLabel = displayLabel; }
    internal AnimalDefaults(string displayLabel, int maxRetries) { DisplayLabel = displayLabel; MaxRetries = maxRetries; }
    protected AnimalDefaults(string displayLabel, bool isProtected) { DisplayLabel = displayLabel; }

    public const int MaxNameLength = 100;
    public const string DefaultPrefix = "Animal_";
    protected const int ProtectedMaxAge = 99;

    public static readonly AnimalKind FallbackKind = AnimalKind.Unknown;
    protected static readonly int ProtectedStaticSeed = 7;

    public static string BuildDefaultName(int id) => $"{DefaultPrefix}{id}";
    protected static string ProtectedStaticFormat(string name) => name.ToUpper();
    protected string ProtectedInstanceMethod() => "protected-instance";
    public virtual string GetLabel() => DisplayLabel;
    public virtual int ComputeScore(int input) => input;
    protected virtual string FormatInternal() => "base";

    public static int MaxAllowed { get; } = 500;
    protected static int ProtectedStaticProp { get; } = 10;
    protected int ProtectedInstanceProp { get; set; } = 1;
    public virtual int VirtualProp { get; set; }

    internal static string InternalFormat(string name) => name.Trim();

    internal const int InternalBatchSize = 50;

    public string InstanceMethod() => "instance";

    public event EventHandler? LabelChanged;
    private string _privateField = "secret";
    private int PrivateProp { get; set; }
    private event EventHandler? PrivateEvent;
    private void PrivateMethod() { }

    public class ValidationRules
    {
        public int MinNameLength { get; set; } = 1;
        public int MaxNameLength { get; set; } = 100;
    }

    internal class CacheOptions
    {
        public int ExpirationSeconds { get; set; } = 300;
    }

    protected class ProtectedInnerConfig
    {
        public int Timeout { get; set; } = 30;
    }

    private class PrivateInner
    {
        public int Secret { get; set; }
    }
}
