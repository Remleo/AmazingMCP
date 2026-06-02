using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceTestsXmlDoc : SymbolInfoServiceTestsBase
{
    [Test]
    public async Task GetTypeDetailsAsync_SourceType_DoesNotContainXmlDoc()
    {
        // Source types have no ISymbol XML doc — only syntax-based doc shown via FileDigest
        var result = await Act("TestProject.Core.Models.Animal");

        // The header line should not contain "///"
        var headerLine = result.Split('\n').First(l => l.Contains("Animal") && l.Contains("source:"));
        headerLine.Should().NotContain("///");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ThirdPartyType_XmlDocAppearsBeforeTypeHeader()
    {
        // AutoMapper.TypeMap has XML doc on the type itself
        var result = await Act("AutoMapper.TypeMap");

        // Doc lines appear before the type header line which ends with "// assembly: ..."
        var lines = result.Split('\n');
        var headerIdx = Array.FindIndex(lines, l => l.Contains("TypeMap") && l.Contains("// assembly:"));
        headerIdx.Should().BeGreaterThan(0, "type header with assembly comment should exist");
        lines[..headerIdx].Should().Contain(l => l.TrimStart().StartsWith("///"));
    }

    [Test]
    public async Task GetTypeDetailsAsync_ThirdPartyType_MultilineXmlDocHasTripleSlashOnEveryLine()
    {
        // IMapper's doc is multi-line — every line must start with "///"
        var result = await Act("AutoMapper.IMapper");

        var lines = result.Split('\n');
        var docLines = lines
            .SkipWhile(l => !l.TrimStart().StartsWith("///"))
            .TakeWhile(l => l.TrimStart().StartsWith("///"))
            .ToList();

        docLines.Should().HaveCountGreaterThan(1, "IMapper XML doc should be multi-line");
        docLines.Should().AllSatisfy(l => l.TrimStart().Should().StartWith("///"));
    }

    [Test]
    public async Task GetTypeDetailsAsync_ThirdPartyType_XmlDocDoesNotContainMemberWrapperTag()
    {
        // GetDocumentationCommentXml() wraps content in <member name="..."> — must be stripped
        var result = await Act("AutoMapper.IMapper");

        result.Should().NotContain("<member ");
        result.Should().NotContain("</member>");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ThirdPartyType_XmlDocContainsSummaryTag()
    {
        // after stripping <member>, the <summary> content should be present
        var result = await Act("AutoMapper.IMapper");

        result.Should().Contain("<summary>");
        result.Should().Contain("</summary>");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ThirdPartyType_MemberXmlDocAppearsBeforeMemberSignature()
    {
        // doc for a member must appear before the member's signature line
        var result = await Act("AutoMapper.IMapper");

        // Find a "/// " line followed by a non-doc line (the member signature)
        result.Should().MatchRegex(@"/// .*\n\s+\S");
    }

    [Test]
    public async Task GetTypeDetailsAsync_ThirdPartyBaseType_XmlDocIsShown()
    {
        // When a third-party type has a third-party base type, the base type's doc should also appear.
        // AutoMapper.Mapper extends internal base — use IMapper which has IMapperBase as interface.
        // IMapperBase is a third-party type and should show its doc.
        var result = await Act("AutoMapper.IMapper");

        // IMapperBase is listed under "Implements:" — its doc should appear there too
        var implementsIndex = result.IndexOf("Implements:", StringComparison.Ordinal);
        implementsIndex.Should().BeGreaterThanOrEqualTo(0);
        var afterImplements = result[implementsIndex..];
        afterImplements.Should().Contain("///");
    }
}
