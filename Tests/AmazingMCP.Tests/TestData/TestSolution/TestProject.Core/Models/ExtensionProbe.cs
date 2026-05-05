namespace TestProject.Core.Models;

/// <summary>
/// Marker type reserved exclusively for testing C# 14 extension block detection
/// in query_usages. Must not be used anywhere else in the test solution.
/// </summary>
public class ExtensionProbe
{
    public string Value { get; set; } = string.Empty;
}
