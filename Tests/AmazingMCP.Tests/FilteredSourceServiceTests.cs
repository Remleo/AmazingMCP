using AmazingMCP.Models;
using AmazingMCP.Services;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public class FilteredSourceServiceTests
{
    IFileStructureService _fileStructure = null!;
    IWildcardPatternFactory _wildcardFactory = null!;
    FilteredSourceService _sut = null!;

    static string TestProjectAppPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "TestData", "TestSolution", "TestProject.App"));

    static string FilePath(params string[] parts) =>
        Path.Combine([TestProjectAppPath, ..parts]);

    [SetUp]
    public void SetUp()
    {
        _fileStructure = Substitute.For<IFileStructureService>();
        _wildcardFactory = Substitute.For<IWildcardPatternFactory>();
        _sut = new FilteredSourceService(_fileStructure, _wildcardFactory);
    }

    // ── file not found ──────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_FileNotFound_ReturnsError()
    {
        var result = _sut.GetFilteredSource("C:\\nonexistent\\file.cs", null);
        result.Should().Contain("File not found");
    }

    [Test]
    public void GetFilteredSource_FileNotFound_DoesNotCallFileStructure()
    {
        _sut.GetFilteredSource("C:\\nonexistent\\file.cs", ["*"]);
        _fileStructure.DidNotReceive().GetItems(Arg.Any<string>());
    }

    // ── no filters ──────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoFilters_ReturnsFullFileContent()
    {
        var path = FilePath("Helpers", "StandaloneHelper.cs");
        var result = _sut.GetFilteredSource(path, null);
        result.Should().Contain("class StandaloneHelper");
        result.Should().NotContain("cut");
    }

    [Test]
    public void GetFilteredSource_EmptyFilters_ReturnsFullFileContent()
    {
        var path = FilePath("Helpers", "StandaloneHelper.cs");
        var result = _sut.GetFilteredSource(path, []);
        result.Should().Contain("class StandaloneHelper");
    }

    [Test]
    public void GetFilteredSource_NoFilters_DoesNotCallFileStructure()
    {
        var path = FilePath("Helpers", "StandaloneHelper.cs");
        _sut.GetFilteredSource(path, null);
        _fileStructure.DidNotReceive().GetItems(Arg.Any<string>());
    }

    // ── with filters — mock interactions ────────────────────────────────────

    [Test]
    public void GetFilteredSource_WithFilters_CallsGetItems()
    {
        var path = FilePath("Helpers", "StandaloneHelper.cs");
        var fullPath = Path.GetFullPath(path);
        _fileStructure.GetItems(fullPath).Returns([]);
        SetupGlobPattern("*Format*", _ => true);

        _sut.GetFilteredSource(path, ["*Format*"]);

        _fileStructure.Received(1).GetItems(fullPath);
    }

    [Test]
    public void GetFilteredSource_WithFilters_CreatesGlobForEachFilter()
    {
        var path = FilePath("Helpers", "StandaloneHelper.cs");
        var fullPath = Path.GetFullPath(path);
        _fileStructure.GetItems(fullPath).Returns([]);
        SetupGlobPattern("*A*", _ => false);
        SetupGlobPattern("*B*", _ => false);

        _sut.GetFilteredSource(path, ["*A*", "*B*"]);

        _wildcardFactory.Received(1).CreateGlob("*A*");
        _wildcardFactory.Received(1).CreateGlob("*B*");
    }

    // ── with filters — matching behavior ────────────────────────────────────

    [Test]
    public void GetFilteredSource_NoMatchingMembers_ReturnsNoMatchesMessage()
    {
        var path = FilePath("Helpers", "StandaloneHelper.cs");
        var fullPath = Path.GetFullPath(path);
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("public class StandaloneHelper", FileStructureItemKind.Type, 3, 10, 3, 3),
            Item("string Format(string input)", FileStructureItemKind.Member, 5, 6, 5, 5)
        ]);
        SetupGlobPattern("*NonExistent*", _ => false);

        var result = _sut.GetFilteredSource(path, ["*NonExistent*"]);

        result.Should().Contain("No matches found");
    }

    [Test]
    public void GetFilteredSource_MatchingMember_IncludesCutMarker()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        var lineCount = File.ReadAllLines(fullPath).Length;
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("namespace TestProject.App.Services", FileStructureItemKind.Namespace, 7, lineCount, 7, 7),
            Item("public class AnimalService : IAnimalService", FileStructureItemKind.Type, 9, lineCount, 9, 10),
            Item("Animal? GetById(int id)", FileStructureItemKind.Member, 25, 26, 25, 25)
        ]);
        SetupGlobPattern("*GetById*", s => s.Contains("GetById"));

        var result = _sut.GetFilteredSource(path, ["*GetById*"]);

        result.Should().Contain("GetById");
        result.Should().Contain("cut");
    }

    [Test]
    public void GetFilteredSource_NamespaceItems_NeverMatched()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        SetupMatchAll();
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("namespace TestProject.App.Services", FileStructureItemKind.Namespace, 7, 42, 7, 7)
        ]);

        var result = _sut.GetFilteredSource(path, ["*"]);

        result.Should().Contain("No matches found");
    }

    [Test]
    public void GetFilteredSource_LargeType_NotMatched()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        SetupMatchAll();
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("public class HugeClass", FileStructureItemKind.Type, 1, 300, 1, 2)
        ]);

        var result = _sut.GetFilteredSource(path, ["*"]);

        result.Should().Contain("No matches found");
    }

    [Test]
    public void GetFilteredSource_SmallType_IsMatched()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        var lineCount = File.ReadAllLines(fullPath).Length;
        SetupMatchAll();
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("public class AnimalService : IAnimalService", FileStructureItemKind.Type, 9, lineCount, 9, 10)
        ]);

        var result = _sut.GetFilteredSource(path, ["*"]);

        result.Should().Contain("class AnimalService");
    }

    // ── declaration headers always visible ──────────────────────────────────

    [Test]
    public void GetFilteredSource_MatchedMember_IncludesTypeDeclaration()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("namespace TestProject.App.Services", FileStructureItemKind.Namespace, 7, 42, 7, 7),
            Item("public class AnimalService : IAnimalService", FileStructureItemKind.Type, 9, 42, 9, 10),
            Item("Animal? GetById(int id)", FileStructureItemKind.Member, 25, 26, 25, 25)
        ]);
        SetupGlobPattern("*GetById*", s => s.Contains("GetById"));

        var result = _sut.GetFilteredSource(path, ["*GetById*"]);

        result.Should().Contain("class AnimalService");
        result.Should().Contain("namespace");
    }

    // ── multiple matches ────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_MultipleMatchingMembers_AllIncluded()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("namespace TestProject.App.Services", FileStructureItemKind.Namespace, 7, 42, 7, 7),
            Item("public class AnimalService : IAnimalService", FileStructureItemKind.Type, 9, 42, 9, 10),
            Item("Animal? GetById(int id)", FileStructureItemKind.Member, 25, 26, 25, 25),
            Item("void Add(Animal animal)", FileStructureItemKind.Member, 31, 41, 31, 31)
        ]);
        SetupMatchAll();

        var result = _sut.GetFilteredSource(path, ["*"]);

        result.Should().Contain("GetById");
        result.Should().Contain("Add(Animal animal)");
    }

    // ── usings item ─────────────────────────────────────────────────────────

    [Test]
    public void GetFilteredSource_UsingsItem_CanBeMatched()
    {
        var path = FilePath("Services", "AnimalService.cs");
        var fullPath = Path.GetFullPath(path);
        _fileStructure.GetItems(fullPath).Returns(
        [
            Item("usings", FileStructureItemKind.Usings, 1, 5, 1, 1),
            Item("namespace TestProject.App.Services", FileStructureItemKind.Namespace, 7, 42, 7, 7)
        ]);
        SetupGlobPattern("usings", s => s == "usings");

        var result = _sut.GetFilteredSource(path, ["usings"]);

        result.Should().Contain("using");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static FileStructureItem Item(
        string symbolString, FileStructureItemKind kind,
        int startLine, int endLine,
        int declLine, int declEndLine) => new()
    {
        SymbolString = symbolString,
        Kind = kind,
        StartLine = startLine,
        EndLine = endLine,
        DeclarationLine = declLine,
        DeclarationEndLine = declEndLine
    };

    void SetupGlobPattern(string pattern, Func<string, bool> matcher)
    {
        var mock = Substitute.For<IWildcardPattern>();
        mock.IsMatch(Arg.Any<string>()).Returns(ci => matcher(ci.Arg<string>()));
        _wildcardFactory.CreateGlob(pattern).Returns(mock);
    }

    void SetupMatchAll()
    {
        var mock = Substitute.For<IWildcardPattern>();
        mock.IsMatch(Arg.Any<string>()).Returns(true);
        _wildcardFactory.CreateGlob(Arg.Any<string>()).Returns(mock);
    }
}
