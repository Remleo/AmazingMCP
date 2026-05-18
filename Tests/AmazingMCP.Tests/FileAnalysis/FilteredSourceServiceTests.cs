using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.Wildcard;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.FileAnalysis;

public class FilteredSourceServiceTests
{
    FilteredSourceService _sut = null!;
    InMemoryFileReader _fileReader = null!;
    const string FilePath = "C:\\fake\\file.cs";

    // ── CS content fixtures ──────────────────────────────────────────────────

    // Small type (< 50 lines): fits entirely when matched
    const string SmallTypeContent = """
        namespace MyApp;

        public class SmallService
        {
            public string GetName() => "hello";

            public int Compute(int x) => x * 2;
        }
        """;

    // Large type (> 50 lines) with xmldoc + attribute: only declaration when matched
    static readonly string LargeTypeContent = BuildLargeTypeContent();

    static string BuildLargeTypeContent()
    {
        var lines = new List<string>
        {
            "namespace MyApp;",
            "",
            "/// <summary>",
            "/// A large service with many methods.",
            "/// </summary>",
            "[Obsolete(\"Use NewService instead\")]",
            "public class LargeService",
            "{"
        };
        for (var i = 1; i <= 50; i++)
            lines.Add($"    public void Method{i}() {{ }}");
        lines.Add("}");
        return string.Join("\n", lines);
    }

    // File with namespace, type, member — for context/cut marker tests
    const string MemberContent = """
        using System;

        namespace MyApp.Services;

        public class AnimalService
        {
            public string GetById(int id) => id.ToString();

            public void Add(string name) { }
        }
        """;

    // File with nested type inside a large outer type
    static readonly string NestedTypeContent = BuildNestedTypeContent();

    static string BuildNestedTypeContent()
    {
        var lines = new List<string>
        {
            "namespace MyApp;",
            "",
            "public class OuterService",
            "{"
        };
        for (var i = 1; i <= 50; i++)
            lines.Add($"    public void Method{i}() {{ }}");
        lines.Add("");
        lines.Add("    public class NestedConfig");
        lines.Add("    {");
        lines.Add("        public int Value { get; set; }");
        lines.Add("    }");
        lines.Add("}");
        return string.Join("\n", lines);
    }

    [SetUp]
    public void SetUp()
    {
        _fileReader = new InMemoryFileReader();
        _sut = new FilteredSourceService(new FileStructureService(_fileReader), new WildcardPatternFactory(), _fileReader);
    }

    // ── file not found ───────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_FileNotFound_ReturnsError()
    {
        // act
        var result = _sut.GetFilteredSource("C:\\nonexistent\\file.cs", null);

        // assert
        result.Should().Contain("File not found");
    }

    // ── no filters ───────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoFilters_ReturnsFullContent()
    {
        // arrange
        _fileReader.Add(FilePath, SmallTypeContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, null);

        // assert
        result.Should().Contain("class SmallService");
        result.Should().NotContain("cut");
    }

    [Test]
    public void GetFilteredSource_EmptyFilters_ReturnsFullContent()
    {
        // arrange
        _fileReader.Add(FilePath, SmallTypeContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, []);

        // assert
        result.Should().Contain("class SmallService");
        result.Should().NotContain("cut");
    }

    // ── no matches ───────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoMatchingMembers_ReturnsNoMatchesMessage()
    {
        // arrange
        _fileReader.Add(FilePath, MemberContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*NonExistent*"]);

        // assert
        result.Should().Contain("No matches found");
    }

    // ── member matching ──────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_MatchingMember_IncludesCutMarker()
    {
        // arrange
        _fileReader.Add(FilePath, MemberContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*GetById*"]);

        // assert
        result.Should().Contain("GetById");
        result.Should().Contain("cut");
    }

    [Test]
    public void GetFilteredSource_MatchingMember_IncludesNamespaceAndTypeDeclaration()
    {
        // arrange
        _fileReader.Add(FilePath, MemberContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*GetById*"]);

        // assert
        result.Should().Contain("namespace MyApp.Services");
        result.Should().Contain("class AnimalService");
        result.Should().Contain("GetById");
    }

    [Test]
    public void GetFilteredSource_MultipleMatchingMembers_AllIncluded()
    {
        // arrange
        _fileReader.Add(FilePath, MemberContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*GetById*", "*Add*"]);

        // assert
        result.Should().Contain("GetById");
        result.Should().Contain("void Add");
    }

    // ── usings ───────────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_UsingsFilter_IncludesUsings()
    {
        // arrange
        _fileReader.Add(FilePath, MemberContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["usings"]);

        // assert
        result.Should().Contain("using System");
    }

    // ── namespace never matched directly ─────────────────────────────────────

    [Test]
    public void GetFilteredSource_NamespaceOnlyFile_ReturnsNoMatches()
    {
        // arrange — file with only a namespace declaration and no members
        const string content = "namespace MyApp.Services;\n";
        _fileReader.Add(FilePath, content);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*"]);

        // assert
        result.Should().Contain("No matches found");
    }

    // ── small type matched entirely ──────────────────────────────────────────

    [Test]
    public void GetFilteredSource_SmallTypeMatched_IncludesFullBody()
    {
        // arrange
        _fileReader.Add(FilePath, SmallTypeContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*SmallService*"]);

        // assert
        result.Should().Contain("GetName");
        result.Should().Contain("Compute");
    }

    // ── large type matched — declaration only ────────────────────────────────

    [Test]
    public void GetFilteredSource_LargeTypeMatched_IncludesOnlyDeclaration()
    {
        // arrange
        _fileReader.Add(FilePath, LargeTypeContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*LargeService*"]);

        // assert — declaration with xmldoc and attribute present
        result.Should().Contain("/// <summary>");
        result.Should().Contain("[Obsolete");
        result.Should().Contain("public class LargeService");
        // body methods must not appear
        result.Should().NotContain("Method1");
        result.Should().NotContain("Method50");
    }

    // ── nested type ──────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NestedTypeNotMatched_NotIncluded()
    {
        // arrange — filter matches only outer type methods, not nested type
        _fileReader.Add(FilePath, NestedTypeContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*Method1*"]);

        // assert
        result.Should().Contain("Method1");
        result.Should().NotContain("NestedConfig");
    }

    [Test]
    public void GetFilteredSource_NestedTypeMatched_IncludesItsDeclaration()
    {
        // arrange
        _fileReader.Add(FilePath, NestedTypeContent);

        // act
        var result = _sut.GetFilteredSource(FilePath, ["*NestedConfig*"]);

        // assert
        result.Should().Contain("NestedConfig");
        result.Should().Contain("class OuterService");
    }
}
