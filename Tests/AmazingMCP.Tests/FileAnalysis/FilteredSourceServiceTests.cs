using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.Wildcard;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.FileAnalysis;

public class FilteredSourceServiceTests
{
    FilteredSourceService _sut = null!;

    // ── CS content fixtures ──────────────────────────────────────────────────

    const string SmallTypeContent = """
        namespace MyApp;

        public class SmallService
        {
            public string GetName() => "hello";

            public int Compute(int x) => x * 2;
        }
        """;

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

    const string MemberContent = """
        using System;

        namespace MyApp.Services;

        public class AnimalService
        {
            public string GetById(int id) => id.ToString();

            public void Add(string name) { }
        }
        """;

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
    public void SetUp() =>
        _sut = new FilteredSourceService(new FileStructureService(), new WildcardPatternFactory());

    // ── no filters ───────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoFilters_ReturnsFullContent()
    {
        // act
        var result = _sut.GetFilteredSource(SmallTypeContent, null);

        // assert
        result.Should().Contain("class SmallService");
        result.Should().NotContain("cut");
    }

    [Test]
    public void GetFilteredSource_EmptyFilters_ReturnsFullContent()
    {
        // act
        var result = _sut.GetFilteredSource(SmallTypeContent, []);

        // assert
        result.Should().Contain("class SmallService");
        result.Should().NotContain("cut");
    }

    // ── no matches ───────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoMatchingMembers_ReturnsNoMatchesMessage()
    {
        // act
        var result = _sut.GetFilteredSource(MemberContent, ["*NonExistent*"]);

        // assert
        result.Should().Contain("No matches found");
    }

    // ── member matching ──────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_MatchingMember_IncludesCutMarker()
    {
        // act
        var result = _sut.GetFilteredSource(MemberContent, ["*GetById*"]);

        // assert
        result.Should().Contain("GetById");
        result.Should().Contain("cut");
    }

    [Test]
    public void GetFilteredSource_MatchingMember_IncludesNamespaceAndTypeDeclaration()
    {
        // act
        var result = _sut.GetFilteredSource(MemberContent, ["*GetById*"]);

        // assert
        result.Should().Contain("namespace MyApp.Services");
        result.Should().Contain("class AnimalService");
        result.Should().Contain("GetById");
    }

    [Test]
    public void GetFilteredSource_MultipleMatchingMembers_AllIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(MemberContent, ["*GetById*", "*Add*"]);

        // assert
        result.Should().Contain("GetById");
        result.Should().Contain("void Add");
    }

    // ── usings ───────────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_UsingsFilter_IncludesUsings()
    {
        // act
        var result = _sut.GetFilteredSource(MemberContent, ["usings"]);

        // assert
        result.Should().Contain("using System");
    }

    // ── namespace never matched directly ─────────────────────────────────────

    [Test]
    public void GetFilteredSource_NamespaceOnlySource_ReturnsNoMatches()
    {
        // arrange
        const string content = "namespace MyApp.Services;\n";

        // act
        var result = _sut.GetFilteredSource(content, ["*"]);

        // assert
        result.Should().Contain("No matches found");
    }

    // ── small type matched entirely ──────────────────────────────────────────

    [Test]
    public void GetFilteredSource_SmallTypeMatched_IncludesFullBody()
    {
        // act
        var result = _sut.GetFilteredSource(SmallTypeContent, ["*SmallService*"]);

        // assert
        result.Should().Contain("GetName");
        result.Should().Contain("Compute");
    }

    // ── large type matched — declaration only ────────────────────────────────

    [Test]
    public void GetFilteredSource_LargeTypeMatched_IncludesOnlyDeclaration()
    {
        // act
        var result = _sut.GetFilteredSource(LargeTypeContent, ["*LargeService*"]);

        // assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("[Obsolete");
        result.Should().Contain("public class LargeService");
        result.Should().NotContain("Method1");
        result.Should().NotContain("Method50");
    }

    // ── nested type ──────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NestedTypeNotMatched_NotIncluded()
    {
        // act
        var result = _sut.GetFilteredSource(NestedTypeContent, ["*Method1*"]);

        // assert
        result.Should().Contain("Method1");
        result.Should().NotContain("NestedConfig");
    }

    [Test]
    public void GetFilteredSource_NestedTypeMatched_IncludesItsDeclaration()
    {
        // act
        var result = _sut.GetFilteredSource(NestedTypeContent, ["*NestedConfig*"]);

        // assert
        result.Should().Contain("NestedConfig");
        result.Should().Contain("class OuterService");
    }
}
