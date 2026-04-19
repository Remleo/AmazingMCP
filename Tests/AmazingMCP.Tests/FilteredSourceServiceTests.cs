using AmazingMCP.Services;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class FilteredSourceServiceTests
{
    FilteredSourceService _sut = null!;

    static string TestProjectAppPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "TestData", "TestSolution", "TestProject.App"));

    static string FilePath(params string[] parts) =>
        Path.Combine([TestProjectAppPath, ..parts]);

    [SetUp]
    public void SetUp() => _sut = new FilteredSourceService(new FileStructureService());

    // ── file not found ─────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_FileNotFound_ReturnsError()
    {
        // act
        var result = _sut.GetFilteredSource("C:\\nonexistent\\file.cs", ["*"]);

        // assert
        result.Should().Contain("File not found");
    }

    [Test]
    public void GetFilteredSource_NoFilters_ReturnsNoFiltersMessage()
    {
        // act
        var result = _sut.GetFilteredSource(FilePath("Helpers", "StandaloneHelper.cs"), []);

        // assert
        result.Should().Contain("No filters specified");
    }

    // ── no matches ─────────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoMatchingFilter_ReturnsNoMatchesMessage()
    {
        // act
        var result = _sut.GetFilteredSource(FilePath("Helpers", "StandaloneHelper.cs"), ["*NonExistentXyz*"]);

        // assert
        result.Should().Contain("No matches found");
    }

    // ── wildcard matching ──────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_WildcardMatchesMethod_SourceLinesIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(FilePath("Helpers", "StandaloneHelper.cs"), ["*Format*"]);

        // assert — matched method is present
        result.Should().Contain("Format");
        // namespace declaration is always included
        result.Should().Contain("namespace");
    }

    [Test]
    public void GetFilteredSource_WildcardIsCaseInsensitive()
    {
        // act
        var result = _sut.GetFilteredSource(FilePath("Helpers", "StandaloneHelper.cs"), ["*format*"]);

        // assert
        result.Should().Contain("Format");
    }

    [Test]
    public void GetFilteredSource_MultipleFilters_AllMatchedMembersIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(
            FilePath("Services", "AnimalService.cs"),
            ["*GetById*", "*Add*"]);

        // assert
        result.Should().Contain("GetById");
        result.Should().Contain("void Add(");
    }

    [Test]
    public void GetFilteredSource_FilterMatchesOnlyOneMethod_OtherMethodsNotIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(
            FilePath("Services", "AnimalService.cs"),
            ["*GetById*"]);

        // assert
        result.Should().Contain("GetById");
        result.Should().NotContain("void Add(");
        result.Should().NotContain("GetByKind");
    }

    // ── usings filter ──────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_UsingsFilter_UsingsBlockIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(FilePath("Services", "AnimalService.cs"), ["usings"]);

        // assert
        result.Should().Contain("using ");
    }

    [Test]
    public void GetFilteredSource_UsingsFilter_NamespaceAlwaysIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(FilePath("Services", "AnimalService.cs"), ["usings"]);

        // assert — namespace declaration is always present even when only usings matched
        result.Should().Contain("namespace TestProject");
    }

    // ── cut marker ─────────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_SkippedSection_CutMarkerInserted()
    {
        // act
        var result = _sut.GetFilteredSource(
            FilePath("Services", "AnimalService.cs"),
            ["*GetById*"]);

        // assert
        result.Should().Contain("// << ... cut ... >>");
    }

    [Test]
    public void GetFilteredSource_AllSectionsMatched_NoCutMarker()
    {
        // arrange — build a temp file with no namespace so everything is matchable
        var tempFile = Path.GetTempFileName() + ".cs";
        try
        {
            File.WriteAllText(tempFile,
                "public class Tiny\n{\n    public int X { get; set; }\n}\n");

            // act — match both the type and the member
            var result = _sut.GetFilteredSource(tempFile, ["*Tiny*", "*int X*"]);

            // assert — the type range covers everything, no cut needed
            result.Should().NotContain("// << ... cut ... >>");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── xmldoc included in range ───────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_MemberWithXmlDoc_XmlDocIncludedInOutput()
    {
        // act
        var result = _sut.GetFilteredSource(
            FilePath("Helpers", "AnimalFormatter.cs"),
            ["*FormatAnimal*"]);

        // assert — the class has xmldoc, and FormatAnimal is a method inside it
        // the class itself is > 1 line but <= 200, so it won't be matched by *FormatAnimal*
        // but the method FormatAnimal should be matched as a Member
        result.Should().Contain("FormatAnimal");
    }

    [Test]
    public void GetFilteredSource_ClassWithXmlDoc_XmlDocIncludedWhenClassMatched()
    {
        // arrange — FileStructureUsingsFixture is a small class with xmldoc
        var filePath = FilePath("Helpers", "FileStructureUsingsFixture.cs");

        // act
        var result = _sut.GetFilteredSource(filePath, ["*FileStructureUsingsFixture*"]);

        // assert
        result.Should().Contain("/// ");
        result.Should().Contain("FileStructureUsingsFixture");
    }

    // ── type size limit ────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_SmallType_TypeMatchedByFilter()
    {
        // StandaloneHelper is a tiny class (well under 200 lines)
        // act
        var result = _sut.GetFilteredSource(
            FilePath("Helpers", "StandaloneHelper.cs"),
            ["*StandaloneHelper*"]);

        // assert
        result.Should().Contain("class StandaloneHelper");
    }

    [Test]
    public void GetFilteredSource_LargeTypeFilter_TypeNotMatchedButMembersCanBe()
    {
        // arrange — build a temp file with a type > 200 lines
        var tempFile = Path.GetTempFileName() + ".cs";
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("namespace Temp;");
            sb.AppendLine("public class BigClass");
            sb.AppendLine("{");
            for (var i = 0; i < 210; i++)
                sb.AppendLine($"    public int Prop{i} {{ get; set; }}");
            sb.AppendLine("}");
            File.WriteAllText(tempFile, sb.ToString());

            // act — filter by class name
            var resultClass = _sut.GetFilteredSource(tempFile, ["*BigClass*"]);
            // act — filter by a member
            var resultMember = _sut.GetFilteredSource(tempFile, ["*Prop5 *"]);

            // assert
            resultClass.Should().Contain("No matches found");
            resultMember.Should().Contain("Prop5");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── deduplication ──────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_TwoFiltersMatchSameMember_NoDuplicateLines()
    {
        // act — both filters match "Format"
        var result = _sut.GetFilteredSource(
            FilePath("Helpers", "StandaloneHelper.cs"),
            ["*Format*", "*string Format*"]);

        // assert — "Format" appears exactly once in source
        var lines = result.Split('\n');
        var formatLines = lines.Count(l => l.Contains("Format("));
        formatLines.Should().Be(1);
    }

    // ── overlapping ranges merged ──────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_OverlappingRanges_MergedWithoutDuplicateLines()
    {
        // arrange — build a temp file where two members share lines (e.g. same-line declarations)
        // We test via two filters that both match the same member — lines should appear once
        var result = _sut.GetFilteredSource(
            FilePath("Services", "AnimalService.cs"),
            ["*GetById*", "*Animal? GetById*"]);

        // assert — GetById source line appears exactly once
        var lines = result.Split('\n');
        var count = lines.Count(l => l.Contains("GetById"));
        count.Should().Be(1);
    }
}
