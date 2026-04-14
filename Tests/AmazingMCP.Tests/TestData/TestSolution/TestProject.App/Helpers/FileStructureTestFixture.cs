using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TestProject.App.Helpers;

/// <summary>
/// Fixture file for FileStructureService / GetFileStructureTool tests.
/// Contains attributes on class, properties, methods and a nested type
/// so tests can assert attribute output without touching production code.
/// </summary>
[Description("A helper with attributes")]
public class FileStructureTestFixture
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Obsolete("Use NewMethod instead")]
    public string OldMethod() => Name;

    [Description("Does the new thing")]
    public string NewMethod() => Name.ToUpperInvariant();

    public class NestedConfig
    {
        [Range(1, 100)]
        public int Value { get; set; }
    }
}
