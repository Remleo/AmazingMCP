using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using NUnit.Framework;
using static AmazingMCP.Tests.Helpers.CompilationHelper;

namespace AmazingMCP.Tests.SymbolQuery;

[Parallelizable(ParallelScope.Self)]
public abstract class SymbolInfoServiceTestsBase
{
    protected SymbolInfoService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new SymbolInfoService(
            new RoslynSymbolService(CreateWorkspaceProvider(cachedSolution), new WildcardPatternFactory()),
            new XmlDocExtractor(),
            new WildcardPatternFactory())
        {
            CompactModeThreshold = 200
        };
    }

    protected async Task<string> Act(string typeName) =>
        await _sut.GetSymbolInfoAsync(CompilationHelper.SolutionPath, typeName);

    protected async Task<string> ActWithFilters(string typeName, string[] memberFilters) =>
        await _sut.GetSymbolInfoAsync(CompilationHelper.SolutionPath, typeName, memberFilters);
}
