using TestProject.Core.Models;

namespace TestProject.App.Helpers;

/// <summary>
/// C# 14 new-style extension block for ExtensionProbe — used to verify that query_usages
/// correctly detects ExtensionProbe as the extended type (TypeAsParameter usage).
/// Note: no call-site usage here — the extension declaration itself is the usage.
/// ExtensionProbe is reserved exclusively for this test scenario.
/// </summary>
public static class ExtensionProbeExtensions
{
    extension(ExtensionProbe probe)
    {
        public string GetDisplayValue() => string.Empty;

        public bool IsEmpty() => false;
    }
}
