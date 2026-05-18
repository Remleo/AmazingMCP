using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class SymbolInfoServiceCompactModeTests
{
    SymbolInfoService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new SymbolInfoService(
            new RoslynSymbolService(CompilationHelper.CreateWorkspaceProvider(cachedSolution), new WildcardPatternFactory()),
            new XmlDocExtractor(),
            new WildcardPatternFactory())
        {
            CompactModeThreshold = 20
        };
    }

    async Task<string> Act(string typeName, string[]? memberFilters = null) =>
        await _sut.GetSymbolInfoAsync(CompilationHelper.SolutionPath, typeName, memberFilters);

    // ── BigType (21 members > threshold 20) ──────────────────────────────────

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_UsesCompactMode()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType");

        // assert
        result.Should().Contain("Only member names are shown");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_CompactMode_ShowsMemberNames()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType");

        // assert
        result.Should().Contain("MethodA");
        result.Should().Contain("PropA");
        result.Should().Contain("ConstA");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_CompactMode_DoesNotShowFullSignatures()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType");

        // assert
        result.Should().NotMatchRegex(@"void\s+MethodA\(\)");
        result.Should().NotMatchRegex(@"int\s+PropA\s*\{");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_CompactMode_ShowsMemberCount()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType");

        // assert
        result.Should().MatchRegex(@"has too many members \(\d+\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_MemberFilters_MatchingFewMembers_ShowsFullSignatures()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType", ["*Get*"]);

        // assert — filtered to ≤20 members → full mode
        result.Should().NotContain("Only member names are shown");
        result.Should().MatchRegex(@"int\s+GetValue\(\)");
        result.Should().MatchRegex(@"string\s+GetName\(\)");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_MemberFilters_ExcludesNonMatchingMembers()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType", ["*Get*"]);

        // assert
        result.Should().NotContain("MethodA");
        result.Should().NotContain("PropA");
    }

    [Test]
    public async Task GetSymbolInfoAsync_TypeWithManyMembers_MemberFilters_StillManyMembers_UsesCompactMode()
    {
        // act — "*" matches all members, still >20 → compact mode
        var result = await Act("TestProject.Core.Models.BigType", ["*"]);

        // assert
        result.Should().Contain("Only member names are shown");
    }

    // ── CompactModeDerivedType (1 member) : CompactModeBaseType (21 members) ─

    [Test]
    public async Task GetSymbolInfoAsync_SmallDerivedType_LargeBaseType_DerivedIsNotCompact()
    {
        // act
        var result = await Act("TestProject.Core.Models.CompactModeDerivedType");

        // assert — derived type itself has 1 member → full mode, signature visible before "Base type:" section
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        var beforeBaseType = baseTypeIdx > 0 ? result[..baseTypeIdx] : result;
        beforeBaseType.Should().MatchRegex(@"int\s+OwnProp");
    }

    [Test]
    public async Task GetSymbolInfoAsync_SmallDerivedType_LargeBaseType_BaseIsCompact()
    {
        // act
        var result = await Act("TestProject.Core.Models.CompactModeDerivedType");

        // assert — base type section uses compact mode
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        baseTypeIdx.Should().BeGreaterThanOrEqualTo(0);
        result[baseTypeIdx..].Should().Contain("Only member names are shown");
    }

    [Test]
    public async Task GetSymbolInfoAsync_SmallDerivedType_LargeBaseType_WarningAppearsOnlyInBaseSection()
    {
        // act
        var result = await Act("TestProject.Core.Models.CompactModeDerivedType");

        // assert — warning appears exactly once (in base type section, not in derived)
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        baseTypeIdx.Should().BeGreaterThanOrEqualTo(0);
        result[..baseTypeIdx].Should().NotContain("Only member names are shown");
        result[baseTypeIdx..].Should().Contain("Only member names are shown");
    }

    // ── CompactModeLargeDerived (21 own members) : CompactModeBaseType (21 members) ─
    // When the derived type is already in compact mode, the base type should also be
    // compact but WITHOUT repeating the warning message (inherited compact mode).

    [Test]
    public async Task GetSymbolInfoAsync_LargeDerivedType_BaseTypeIsAlsoCompact_NoWarningRepeated()
    {
        // act
        var result = await Act("TestProject.Core.Models.CompactModeLargeDerived");

        // assert — derived is compact (has warning)
        result.Should().Contain("Only member names are shown");

        // assert — base type section exists and is also compact
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        baseTypeIdx.Should().BeGreaterThanOrEqualTo(0);
        result[baseTypeIdx..].Should().Contain("CompactModeBaseType");

        // assert — warning appears exactly once (not repeated for base type)
        var warningCount = System.Text.RegularExpressions.Regex
            .Matches(result, "Only member names are shown")
            .Count;
        warningCount.Should().Be(1);
    }

    [Test]
    public async Task GetSymbolInfoAsync_LargeDerivedType_BaseTypeIsAlsoCompact_BaseMemberNamesShown()
    {
        // act
        var result = await Act("TestProject.Core.Models.CompactModeLargeDerived");

        // assert — base type members are shown as names (compact), not full signatures
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        baseTypeIdx.Should().BeGreaterThanOrEqualTo(0);
        var afterBaseType = result[baseTypeIdx..];
        afterBaseType.Should().Contain("PropA");
        afterBaseType.Should().NotMatchRegex(@"int\s+PropA\s*\{");
    }

    // ── memberFilters applied to base types ──────────────────────────────────

    [Test]
    public async Task GetSymbolInfoAsync_MemberFilters_AppliedToBaseType()
    {
        // arrange — CompactModeDerivedType (1 member) : CompactModeBaseType (21 members)
        // with filter "Prop*" base type has only 6 members → full mode, signatures visible
        // act
        var result = await Act("TestProject.Core.Models.CompactModeDerivedType", ["Prop*"]);

        // assert — base type section shows full signatures for Prop* members
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        baseTypeIdx.Should().BeGreaterThanOrEqualTo(0);
        var afterBaseType = result[baseTypeIdx..];
        afterBaseType.Should().NotContain("Only member names are shown");
        afterBaseType.Should().MatchRegex(@"int\s+PropA");

        // assert — non-matching members not shown in base type section
        afterBaseType.Should().NotContain("MethodA");
    }

    [Test]
    public async Task GetSymbolInfoAsync_MemberFilters_BaseTypeStillCompact_WhenFilterMatchesTooMany()
    {
        // arrange — filter "*" matches all 21 members of base → still compact
        // act
        var result = await Act("TestProject.Core.Models.CompactModeDerivedType", ["*"]);

        // assert — base type section is still compact
        var baseTypeIdx = result.IndexOf("Base type:", StringComparison.Ordinal);
        baseTypeIdx.Should().BeGreaterThanOrEqualTo(0);
        result[baseTypeIdx..].Should().Contain("Only member names are shown");
    }

    [Test]
    public async Task GetSymbolInfoAsync_MemberFilters_FooterNoteShown()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType", ["*Get*"]);

        // assert — footer note lists the applied filters
        result.Should().Contain("Output is filtered");
        result.Should().Contain("\"*Get*\"");
    }

    [Test]
    public async Task GetSymbolInfoAsync_NoMemberFilters_FooterNoteNotShown()
    {
        // act
        var result = await Act("TestProject.Core.Models.BigType");

        // assert
        result.Should().NotContain("Output is filtered");
    }
}
