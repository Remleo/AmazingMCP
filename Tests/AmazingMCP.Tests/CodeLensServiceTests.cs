using AmazingMCP.Services;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace AmazingMCP.Tests;

/// <summary>
/// Integration tests for <see cref="CodeLensService"/> against the real TestSolution.
/// Split into partial files by section: Variables, Fields, Properties, Calls, Extensions, Definitions, Misc.
/// </summary>
[TestFixture]
public partial class CodeLensServiceTests
{
    CodeLensService _sut = null!;
    IWorkspaceProvider _workspaceProvider = null!;

    static readonly string FixturePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "TestData", "TestSolution",
        "TestProject.App", "Helpers", "CodeLensTestFixture.cs"));

    static readonly string PrimaryCtorFixturePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "TestData", "TestSolution",
        "TestProject.App", "Helpers", "PrimaryCtorTestFixture.cs"));

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();

        _workspaceProvider = Substitute.For<IWorkspaceProvider>();
        _workspaceProvider
            .GetSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(cachedSolution);

        _sut = new CodeLensService(_workspaceProvider);
    }

    async Task<string> Act(int startLine, int endLine)
        => await _sut.AnalyzeAsync(CompilationHelper.SolutionPath, FixturePath, startLine, endLine);

    async Task<string> ActPrimaryCtor(int startLine, int endLine)
        => await _sut.AnalyzeAsync(CompilationHelper.SolutionPath, PrimaryCtorFixturePath, startLine, endLine);

    static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Strips the leading source snippet (```csharp ... ```) from the output,
    /// returning only the analysis sections.
    /// </summary>
    static string StripSourceSnippet(string result)
    {
        const string fence = "```";
        var first = result.IndexOf(fence, StringComparison.Ordinal);
        if (first < 0) return result;
        var closing = result.IndexOf(fence, first + fence.Length, StringComparison.Ordinal);
        if (closing < 0) return result;
        return result[(closing + fence.Length)..];
    }
}
