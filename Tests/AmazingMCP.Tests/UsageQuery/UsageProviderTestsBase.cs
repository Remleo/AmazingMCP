using AmazingMCP.Configuration;
using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using static AmazingMCP.Tests.Helpers.CompilationHelper;

namespace AmazingMCP.Tests.UsageQuery;

[Parallelizable(ParallelScope.Self)]
public abstract class UsageProviderTestsBase
{
    protected IUsageProvider _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new UsageProvider(
            CreateWorkspaceProvider(cachedSolution),
            new WildcardPatternFactory(),
            new InheritanceUsageProvider(
                new InheritanceSearchSymbolResolver(CreateTypeProvider(), CreateVersionedStrategy()),
                new RoslynDerivedTypeService(CreateTypeProvider(), CreateAllInstancesStrategy())),
            Options.Create(new QueryUsagesOptions()));
    }

    protected async Task<IReadOnlyList<UsageMatch>> Act(
        string typeName,
        string? predicate = null,
        string[]? scanInclude = null,
        string[]? scanExclude = null)
    {
        var (matches, error, _) = await _sut.QueryAsync(
            CompilationHelper.SolutionPath,
            typeName,
            predicate,
            scanInclude,
            scanExclude);

        error.Should().BeNull();
        return matches;
    }
}
