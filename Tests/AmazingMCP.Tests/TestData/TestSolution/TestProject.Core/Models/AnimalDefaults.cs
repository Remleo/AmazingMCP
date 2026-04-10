namespace TestProject.Core.Models;

public class AnimalDefaults
{
    public const int MaxNameLength = 100;
    public const string DefaultPrefix = "Animal_";

    public static readonly AnimalKind FallbackKind = AnimalKind.Unknown;

    public static string BuildDefaultName(int id) => $"{DefaultPrefix}{id}";

    public static int MaxAllowed { get; } = 500;

    internal static string InternalFormat(string name) => name.Trim();

    internal const int InternalBatchSize = 50;

    public string InstanceMethod() => "instance";

    public class ValidationRules
    {
        public int MinNameLength { get; set; } = 1;
        public int MaxNameLength { get; set; } = 100;
    }

    internal class CacheOptions
    {
        public int ExpirationSeconds { get; set; } = 300;
    }

    private class PrivateInner
    {
        public int Secret { get; set; }
    }
}
