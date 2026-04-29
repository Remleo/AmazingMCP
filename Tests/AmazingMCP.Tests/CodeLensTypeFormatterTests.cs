using AmazingMCP.Services.CodeLens;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

[TestFixture]
public class CodeLensTypeFormatterTests
{
    // ── Non-generic ───────────────────────────────────────────────────────

    [Test]
    public void TrimSystemNamespace_NonSystemType_Unchanged()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace("MyApp.Core.Animal");

        // assert
        result.Should().Be("MyApp.Core.Animal");
    }

    [Test]
    public void TrimSystemNamespace_SystemType_NamespaceStripped()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace("System.String");

        // assert
        result.Should().Be("String");
    }

    [Test]
    public void TrimSystemNamespace_DeepSystemNamespace_StrippedToShortName()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace("System.Threading.CancellationToken");

        // assert
        result.Should().Be("CancellationToken");
    }

    // ── Simple generics ───────────────────────────────────────────────────

    [Test]
    public void TrimSystemNamespace_SystemGenericWithNonSystemArg_OuterStripped()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.IEnumerable<MyApp.Core.Animal>");

        // assert
        result.Should().Be("IEnumerable<MyApp.Core.Animal>");
    }

    [Test]
    public void TrimSystemNamespace_SystemGenericWithSystemArg_BothStripped()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.List<System.String>");

        // assert
        result.Should().Be("List<String>");
    }

    [Test]
    public void TrimSystemNamespace_NonSystemGenericWithSystemArg_ArgStripped()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "MyApp.Core.Repository<System.String>");

        // assert
        result.Should().Be("MyApp.Core.Repository<String>");
    }

    // ── Multiple generic arguments ────────────────────────────────────────

    [Test]
    public void TrimSystemNamespace_SystemGenericWithMultipleArgs_AllStripped()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.Dictionary<System.String, System.Int32>");

        // assert
        result.Should().Be("Dictionary<String, Int32>");
    }

    [Test]
    public void TrimSystemNamespace_MixedArgs_OnlySystemArgsStripped()
    {
        // act
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.Dictionary<MyApp.Core.Animal, System.Int32>");

        // assert
        result.Should().Be("Dictionary<MyApp.Core.Animal, Int32>");
    }

    // ── Nested generics ───────────────────────────────────────────────────

    [Test]
    public void TrimSystemNamespace_NestedSystemGenericInSystemGeneric_AllStripped()
    {
        // act — Dictionary<List<Int32>, String>
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.Dictionary<System.Collections.Generic.List<System.Int32>, System.String>");

        // assert
        result.Should().Be("Dictionary<List<Int32>, String>");
    }

    [Test]
    public void TrimSystemNamespace_NestedNonSystemGenericInSystemGeneric_OuterStripped()
    {
        // act — IEnumerable<Dictionary<Animal, Int32>>
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.IEnumerable<System.Collections.Generic.Dictionary<MyApp.Core.Animal, System.Int32>>");

        // assert
        result.Should().Be("IEnumerable<Dictionary<MyApp.Core.Animal, Int32>>");
    }

    [Test]
    public void TrimSystemNamespace_DeeplyNestedGenerics_AllLevelsHandled()
    {
        // act — Func<IEnumerable<List<Animal>>, CancellationToken, Task>
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Func<System.Collections.Generic.IEnumerable<System.Collections.Generic.List<MyApp.Core.Animal>>, System.Threading.CancellationToken, System.Threading.Tasks.Task>");

        // assert
        result.Should().Be("Func<IEnumerable<List<MyApp.Core.Animal>>, CancellationToken, Task>");
    }

    [Test]
    public void TrimSystemNamespace_NestedGenericAsSecondArg_BothArgsHandled()
    {
        // act — Dictionary<String, List<Animal>>
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<MyApp.Core.Animal>>");

        // assert
        result.Should().Be("Dictionary<String, List<MyApp.Core.Animal>>");
    }

    [Test]
    public void TrimSystemNamespace_TwoNestedGenericsAsArgs_BothHandled()
    {
        // act — Dictionary<List<Animal>, IEnumerable<String>>
        var result = CodeLensTypeFormatter.TrimSystemNamespace(
            "System.Collections.Generic.Dictionary<System.Collections.Generic.List<MyApp.Core.Animal>, System.Collections.Generic.IEnumerable<System.String>>");

        // assert
        result.Should().Be("Dictionary<List<MyApp.Core.Animal>, IEnumerable<String>>");
    }
}
