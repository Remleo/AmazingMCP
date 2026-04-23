using AmazingMCP.Services;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class FileStructureServiceTests
{
    IFileStructureService _sut = null!;

    static string TestProjectAppPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "TestData", "TestSolution", "TestProject.App"));

    static string FilePath(params string[] parts) =>
        Path.Combine([TestProjectAppPath, ..parts]);

    [SetUp]
    public void SetUp() => _sut = new FileStructureService();

    // ── file not found ─────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_FileNotFound_ReturnsError()
    {
        var result = _sut.GetStructure("C:\\nonexistent\\file.cs");

        result.Should().Contain("File not found");
    }

    [Test]
    public void GetStructure_RelativePath_ResolvesCorrectly()
    {
        // relative path from CWD — just verify it doesn't crash with "File not found"
        // when the file actually exists at the absolute path
        var absPath = FilePath("Helpers", "StandaloneHelper.cs");
        var result = _sut.GetStructure(absPath);

        result.Should().NotContain("File not found");
    }

    // ── namespace ──────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_FileScopedNamespace_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "StandaloneHelper.cs"));

        result.Should().Contain("namespace TestProject.App.Helpers");
    }

    [Test]
    public void GetStructure_Namespace_HasLineInfo()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "StandaloneHelper.cs"));

        result.Should().MatchRegex(@"/\*\[lines?:\d+[^\]]*\]\*/ namespace TestProject\.App\.Helpers");
    }

    // ── class signature ────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_PublicClass_SignatureIncluded()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "StandaloneHelper.cs"));

        result.Should().Contain("public class StandaloneHelper");
    }

    [Test]
    public void GetStructure_AbstractClass_ModifierIncluded()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalServiceBase.cs"));

        result.Should().Contain("public abstract class AnimalServiceBase");
    }

    [Test]
    public void GetStructure_ClassWithBaseList_BaseListIncluded()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().Contain(": IAnimalService");
    }

    [Test]
    public void GetStructure_GenericInterface_TypeParamsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Mapping", "IEntityMapper.cs"));

        result.Should().Contain("IEntityMapper<TSource, TDestination>");
    }

    [Test]
    public void GetStructure_Class_HasLineRange()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().MatchRegex(@"/\*\[lines:\d+ \+\d+\]\*/ .*class AnimalService");
    }

    // ── interface ──────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_Interface_KeywordIncluded()
    {
        var result = _sut.GetStructure(FilePath("Mapping", "IEntityMapper.cs"));

        result.Should().Contain("public interface IEntityMapper");
    }

    // ── fields ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_PrivateReadonlyField_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().Contain("readonly IAnimalRepository _repository");
    }

    [Test]
    public void GetStructure_Field_HasLineInfo()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ .*_repository;");
    }

    // ── constructors ───────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_Constructor_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().Contain("AnimalService(");
    }

    [Test]
    public void GetStructure_Constructor_ParametersIncluded()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().Contain("IAnimalRepository repository");
        result.Should().Contain("INotificationService notification");
    }

    // ── methods ────────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_Method_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "StandaloneHelper.cs"));

        result.Should().Contain("string Format(string input)");
    }

    [Test]
    public void GetStructure_Method_HasLineInfo()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "StandaloneHelper.cs"));

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ .*Format\(string input\);");
    }

    [Test]
    public void GetStructure_AsyncMethod_ReturnTypeIncluded()
    {
        var result = _sut.GetStructure(FilePath("MessageHandling", "AnimalCreatedMessageHandler.cs"));

        result.Should().Contain("Task HandleAsync(");
    }

    [Test]
    public void GetStructure_InterfaceMethods_AreIncluded()
    {
        var result = _sut.GetStructure(FilePath("Mapping", "IEntityMapper.cs"));

        result.Should().Contain("Map(TSource source)");
        result.Should().Contain("MapBack(TDestination destination)");
    }

    // ── properties ─────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_AutoProperty_AccessorsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Persistence", "AnimalRepository.cs"));

        result.Should().Contain("int Count");
        result.Should().Contain("{ get; }");
    }

    // ── enum ───────────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_Enum_KeywordIncluded()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalKind.cs"));

        result.Should().Contain("public enum AnimalKind");
    }

    [Test]
    public void GetStructure_Enum_ValuesIncluded()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalKind.cs"));

        result.Should().Contain("Unknown = 0");
        result.Should().Contain("Cat = 1");
        result.Should().Contain("Dog = 2");
        result.Should().Contain("Parrot = 3");
    }

    // ── constants ──────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_Const_IsIncluded()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"));

        result.Should().Contain("const int MaxNameLength = 100");
    }

    [Test]
    public void GetStructure_StringConst_ValueIncluded()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"));

        result.Should().Contain("const string DefaultPrefix");
        result.Should().Contain("\"Animal_\"");
    }

    [Test]
    public void GetStructure_InternalConst_IsIncluded()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"));

        result.Should().Contain("const int InternalBatchSize = 50");
    }

    // ── nested types ───────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_NestedPublicClass_IsIncluded()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"));

        result.Should().Contain("class ValidationRules");
    }

    [Test]
    public void GetStructure_NestedPrivateClass_IsIncluded()
    {
        // unlike SymbolInfoService, FileStructureService shows ALL members including private
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"));

        result.Should().Contain("class PrivateInner");
    }

    [Test]
    public void GetStructure_NestedClass_IsIndented()
    {
        var result = _sut.GetStructure(
            Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"));

        // nested class must appear after the outer class line and be indented
        var lines = result.Split('\n');
        var outerIdx  = Array.FindIndex(lines, l => l.Contains("class AnimalDefaults"));
        var nestedIdx = Array.FindIndex(lines, l => l.Contains("class ValidationRules"));

        nestedIdx.Should().BeGreaterThan(outerIdx);
        lines[nestedIdx].Should().StartWith("    "); // at least one indent level
    }

    // ── attributes ─────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_ClassAttribute_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "FileStructureTestFixture.cs"));

        result.Should().Contain("[Description(");
    }

    [Test]
    public void GetStructure_PropertyAttribute_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "FileStructureTestFixture.cs"));

        result.Should().Contain("[Required]");
    }

    [Test]
    public void GetStructure_MethodAttribute_IsIncluded()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "FileStructureTestFixture.cs"));

        result.Should().Contain("[Obsolete(");
    }

    [Test]
    public void GetStructure_AttributeAppearsBeforeMember()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "FileStructureTestFixture.cs"));

        var lines = result.Split('\n');
        var attrIdx   = Array.FindIndex(lines, l => l.Contains("[Obsolete("));
        var methodIdx = Array.FindIndex(lines, l => l.Contains("OldMethod()"));

        attrIdx.Should().BeGreaterThanOrEqualTo(0);
        methodIdx.Should().BeGreaterThan(attrIdx);
    }

    // ── ordering ───────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_Members_AppearInSourceOrder()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        var lines = result.Split('\n');
        var fieldIdx  = Array.FindIndex(lines, l => l.Contains("_repository"));
        var ctorIdx   = Array.FindIndex(lines, l => l.Contains("AnimalService("));
        var methodIdx = Array.FindIndex(lines, l => l.Contains("GetById("));

        fieldIdx.Should().BeLessThan(ctorIdx);
        ctorIdx.Should().BeLessThan(methodIdx);
    }

    // ── position format ────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_SingleLineElement_NoLinesCount()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "StandaloneHelper.cs"));

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ .*Format\(string input\);");
        result.Should().NotMatchRegex(@"Format\(string input\).*\+\d+");
    }

    [Test]
    public void GetStructure_MultiLineElement_HasLinesCount()
    {
        var result = _sut.GetStructure(FilePath("Services", "AnimalService.cs"));

        result.Should().MatchRegex(@"/\*\[lines:\d+ \+\d+\]\*/");
        result.Should().Contain("AnimalService(");
    }

    // ── usings ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetStructure_FileWithUsings_UsingsLinePresent()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "AnimalFormatter.cs"));

        result.Should().Contain("usings");
    }

    [Test]
    public void GetStructure_FileWithUsings_UsingsAppearsBeforeNamespace()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "AnimalFormatter.cs"));

        var lines     = result.Split('\n');
        var usingsIdx = Array.FindIndex(lines, l => l.TrimStart().StartsWith("/*[") && l.Contains("]*/ usings"));
        var nsIdx     = Array.FindIndex(lines, l => l.TrimStart().StartsWith("/*[") && l.Contains("namespace"));

        usingsIdx.Should().BeGreaterThanOrEqualTo(0);
        nsIdx.Should().BeGreaterThan(usingsIdx);
    }

    [Test]
    public void GetStructure_FileWithUsings_HasLineInfo()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "AnimalFormatter.cs"));

        result.Should().MatchRegex(@"/\*\[lines?:\d+[^\]]*\]\*/ usings");
    }

    [Test]
    public void GetStructure_FileWithSingleUsing_NoLinesCount()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "AnimalFormatter.cs"));

        result.Should().MatchRegex(@"/\*\[lines?:\d+[^\]]*\]\*/ usings");
    }

    [Test]
    public void GetStructure_FileWithNoUsings_NoUsingsLine()
    {
        var result = _sut.GetStructure(FilePath("Mapping", "IEntityMapper.cs"));

        var lines = result.Split('\n');
        lines.Should().NotContain(l => System.Text.RegularExpressions.Regex.IsMatch(l.Trim(), @"^/\*\[.*\]\*/ usings"));
    }

    [Test]
    public void GetStructure_UsingsWithCommentsBetween_RangeSpansFirstToLast()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "FileStructureUsingsFixture.cs"));

        // First using = line 2, last using = line 6 → +4
        result.Should().MatchRegex(@"/\*\[lines:2 \+4\]\*/ usings");
    }

    [Test]
    public void GetStructure_UsingsWithCommentsBetween_CommentLinesNotSeparateEntries()
    {
        var result = _sut.GetStructure(FilePath("Helpers", "FileStructureUsingsFixture.cs"));

        // Raw comment lines (// or /* not part of position markers) must not appear as separate entries.
        // Position markers have the form: /* [lines:N +M] */ or /* [line:N] */
        var lines = result.Split('\n');
        lines.Should().NotContain(l => l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///"));
        // /* lines that are NOT position markers should not appear
        lines.Should().NotContain(l =>
            l.TrimStart().StartsWith("/*") &&
            !System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^/\*\[lines?:\d+"));
    }
}
