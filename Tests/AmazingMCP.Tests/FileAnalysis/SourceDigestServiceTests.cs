using AmazingMCP.Services.FileAnalysis;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.FileAnalysis;

public class SourceDigestServiceTests
{
    ISourceDigestService _sut = null!;

    static string TestProjectAppPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "TestData", "TestSolution", "TestProject.App"));

    static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine([TestProjectAppPath, ..parts]));

    static string SourceAt(string relativePath) =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, relativePath)));

    [SetUp]
    public void SetUp() => _sut = new SourceDigestService(new XmlDocExtractor());

    // ── file-scoped namespace ──────────────────────────────────────────────────

    [Test]
    public void GetDigest_FileScopedNamespace_HasSingleLinePosition()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ namespace TestProject\.App\.Helpers");
    }

    [Test]
    public void GetDigest_FileScopedNamespace_MembersNotIndented()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        var lines = result.Split('\n');
        var classLine = lines.FirstOrDefault(l => l.Contains("class StandaloneHelper"));

        classLine.Should().NotBeNull();
        classLine!.Should().NotStartWith("    ");
    }

    // ── namespace ──────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_FileScopedNamespace_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().Contain("namespace TestProject.App.Helpers");
    }

    [Test]
    public void GetDigest_Namespace_HasLineInfo()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[lines?:\d+[^\]]*\]\*/ namespace TestProject\.App\.Helpers");
    }

    // ── class signature ────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_PublicClass_SignatureIncluded()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().Contain("public class StandaloneHelper");
    }

    [Test]
    public void GetDigest_AbstractClass_ModifierIncluded()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalServiceBase.cs"), includeLineNumbers: true);

        result.Should().Contain("public abstract class AnimalServiceBase");
    }

    [Test]
    public void GetDigest_ClassWithBaseList_BaseListIncluded()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().Contain(": IAnimalService");
    }

    [Test]
    public void GetDigest_GenericInterface_TypeParamsIncluded()
    {
        var result = _sut.GetDigest(Source("Mapping", "IEntityMapper.cs"), includeLineNumbers: true);

        result.Should().Contain("IEntityMapper<TSource, TDestination>");
    }

    [Test]
    public void GetDigest_Class_HasLineRange()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[lines:\d+ \+\d+\]\*/ .*class AnimalService");
    }

    // ── interface ──────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_Interface_KeywordIncluded()
    {
        var result = _sut.GetDigest(Source("Mapping", "IEntityMapper.cs"), includeLineNumbers: true);

        result.Should().Contain("public interface IEntityMapper");
    }

    // ── fields ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_PrivateReadonlyField_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().Contain("readonly IAnimalRepository _repository");
    }

    [Test]
    public void GetDigest_Field_HasLineInfo()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ .*_repository;");
    }

    // ── constructors ───────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_Constructor_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().Contain("AnimalService(");
    }

    [Test]
    public void GetDigest_Constructor_ParametersIncluded()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().Contain("IAnimalRepository repository");
        result.Should().Contain("INotificationService notification");
    }

    // ── methods ────────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_Method_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().Contain("string Format(string input)");
    }

    [Test]
    public void GetDigest_Method_HasLineInfo()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ .*Format\(string input\);");
    }

    [Test]
    public void GetDigest_AsyncMethod_ReturnTypeIncluded()
    {
        var result = _sut.GetDigest(Source("MessageHandling", "AnimalCreatedMessageHandler.cs"), includeLineNumbers: true);

        result.Should().Contain("Task HandleAsync(");
    }

    [Test]
    public void GetDigest_InterfaceMethods_AreIncluded()
    {
        var result = _sut.GetDigest(Source("Mapping", "IEntityMapper.cs"), includeLineNumbers: true);

        result.Should().Contain("Map(TSource source)");
        result.Should().Contain("MapBack(TDestination destination)");
    }

    // ── properties ─────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_AutoProperty_AccessorsIncluded()
    {
        var result = _sut.GetDigest(Source("Persistence", "AnimalRepository.cs"), includeLineNumbers: true);

        result.Should().Contain("int Count");
        result.Should().Contain("{ get; }");
    }

    // ── enum ───────────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_Enum_KeywordIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalKind.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("public enum AnimalKind");
    }

    [Test]
    public void GetDigest_Enum_ValuesIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalKind.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("Unknown = 0");
        result.Should().Contain("Cat = 1");
        result.Should().Contain("Dog = 2");
        result.Should().Contain("Parrot = 3");
    }

    // ── constants ──────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_Const_IsIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("const int MaxNameLength = 100");
    }

    [Test]
    public void GetDigest_StringConst_ValueIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("const string DefaultPrefix");
        result.Should().Contain("\"Animal_\"");
    }

    [Test]
    public void GetDigest_InternalConst_IsIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("const int InternalBatchSize = 50");
    }

    // ── nested types ───────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_NestedPublicClass_IsIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("class ValidationRules");
    }

    [Test]
    public void GetDigest_NestedPrivateClass_IsIncluded()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"))),
            includeLineNumbers: true);

        result.Should().Contain("class PrivateInner");
    }

    [Test]
    public void GetDigest_NestedClass_IsIndented()
    {
        var result = _sut.GetDigest(
            File.ReadAllText(Path.GetFullPath(Path.Combine(TestProjectAppPath, "..", "TestProject.Core", "Models", "AnimalDefaults.cs"))),
            includeLineNumbers: true);

        var lines = result.Split('\n');
        var outerIdx  = Array.FindIndex(lines, l => l.Contains("class AnimalDefaults"));
        var nestedIdx = Array.FindIndex(lines, l => l.Contains("class ValidationRules"));

        nestedIdx.Should().BeGreaterThan(outerIdx);
        lines[nestedIdx].Should().StartWith("    ");
    }

    // ── attributes ─────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_ClassAttribute_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Helpers", "FileStructureTestFixture.cs"), includeLineNumbers: true);

        result.Should().Contain("[Description(");
    }

    [Test]
    public void GetDigest_PropertyAttribute_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Helpers", "FileStructureTestFixture.cs"), includeLineNumbers: true);

        result.Should().Contain("[Required]");
    }

    [Test]
    public void GetDigest_MethodAttribute_IsIncluded()
    {
        var result = _sut.GetDigest(Source("Helpers", "FileStructureTestFixture.cs"), includeLineNumbers: true);

        result.Should().Contain("[Obsolete(");
    }

    [Test]
    public void GetDigest_AttributeAppearsBeforeMember()
    {
        var result = _sut.GetDigest(Source("Helpers", "FileStructureTestFixture.cs"), includeLineNumbers: true);

        var lines = result.Split('\n');
        var attrIdx   = Array.FindIndex(lines, l => l.Contains("[Obsolete("));
        var methodIdx = Array.FindIndex(lines, l => l.Contains("OldMethod()"));

        attrIdx.Should().BeGreaterThanOrEqualTo(0);
        methodIdx.Should().BeGreaterThan(attrIdx);
    }

    // ── ordering ───────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_Members_AppearInSourceOrder()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        var lines = result.Split('\n');
        var fieldIdx  = Array.FindIndex(lines, l => l.Contains("_repository"));
        var ctorIdx   = Array.FindIndex(lines, l => l.Contains("AnimalService("));
        var methodIdx = Array.FindIndex(lines, l => l.Contains("GetById("));

        fieldIdx.Should().BeLessThan(ctorIdx);
        ctorIdx.Should().BeLessThan(methodIdx);
    }

    // ── position format ────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_SingleLineElement_NoLinesCount()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[line:\d+\]\*/ .*Format\(string input\);");
        result.Should().NotMatchRegex(@"Format\(string input\).*\+\d+");
    }

    [Test]
    public void GetDigest_MultiLineElement_HasLinesCount()
    {
        var result = _sut.GetDigest(Source("Services", "AnimalService.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[lines:\d+ \+\d+\]\*/");
        result.Should().Contain("AnimalService(");
    }

    // ── usings ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetDigest_FileWithUsings_UsingsLinePresent()
    {
        var result = _sut.GetDigest(Source("Helpers", "AnimalFormatter.cs"), includeLineNumbers: true);

        result.Should().Contain("usings");
    }

    [Test]
    public void GetDigest_FileWithUsings_UsingsAppearsBeforeNamespace()
    {
        var result = _sut.GetDigest(Source("Helpers", "AnimalFormatter.cs"), includeLineNumbers: true);

        var lines     = result.Split('\n');
        var usingsIdx = Array.FindIndex(lines, l => l.TrimStart().StartsWith("/*[") && l.Contains("]*/ usings"));
        var nsIdx     = Array.FindIndex(lines, l => l.TrimStart().StartsWith("/*[") && l.Contains("namespace"));

        usingsIdx.Should().BeGreaterThanOrEqualTo(0);
        nsIdx.Should().BeGreaterThan(usingsIdx);
    }

    [Test]
    public void GetDigest_FileWithUsings_HasLineInfo()
    {
        var result = _sut.GetDigest(Source("Helpers", "AnimalFormatter.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[lines?:\d+[^\]]*\]\*/ usings");
    }

    [Test]
    public void GetDigest_FileWithNoUsings_NoUsingsLine()
    {
        var result = _sut.GetDigest(Source("Mapping", "IEntityMapper.cs"), includeLineNumbers: true);

        var lines = result.Split('\n');
        lines.Should().NotContain(l => System.Text.RegularExpressions.Regex.IsMatch(l.Trim(), @"^/\*\[.*\]\*/ usings"));
    }

    [Test]
    public void GetDigest_UsingsWithCommentsBetween_RangeSpansFirstToLast()
    {
        var result = _sut.GetDigest(Source("Helpers", "FileStructureUsingsFixture.cs"), includeLineNumbers: true);

        result.Should().MatchRegex(@"/\*\[lines:2 \+4\]\*/ usings");
    }

    [Test]
    public void GetDigest_UsingsWithCommentsBetween_CommentLinesNotSeparateEntries()
    {
        var result = _sut.GetDigest(Source("Helpers", "FileStructureUsingsFixture.cs"), includeLineNumbers: true);

        var lines = result.Split('\n');
        lines.Should().NotContain(l => l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///"));
        lines.Should().NotContain(l =>
            l.TrimStart().StartsWith("/*") &&
            !System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^/\*\[lines?:\d+"));
    }

    // ── includeLineNumbers: false ──────────────────────────────────────────────

    [Test]
    public void GetDigest_WithoutLineNumbers_NoLineAnnotations()
    {
        var result = _sut.GetDigest(Source("Helpers", "StandaloneHelper.cs"), includeLineNumbers: false);

        result.Should().NotMatchRegex(@"/\*\[line");
        result.Should().Contain("class StandaloneHelper");
    }

    [Test]
    public void GetDigest_WithoutLineNumbers_UsingsLinePresent()
    {
        var result = _sut.GetDigest(Source("Helpers", "AnimalFormatter.cs"), includeLineNumbers: false);

        result.Should().Contain("usings");
        result.Should().NotContain("/*[");
    }
}
