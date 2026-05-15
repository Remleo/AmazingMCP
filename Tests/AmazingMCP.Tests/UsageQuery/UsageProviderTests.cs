using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;
using AmazingMCP.Models.Workspace;
using AmazingMCP.Services;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Tests.Helpers;
using static AmazingMCP.Tests.Helpers.CompilationHelper;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

public partial class UsageProviderTests
{
    CachedSolution _cachedSolution = null!;
    IUsageProvider _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cachedSolution = await CompilationHelper.GetSharedSolutionAsync();
        _sut = new UsageProvider(
            CreateWorkspaceProvider(_cachedSolution),
            new WildcardPatternFactory(),
            Microsoft.Extensions.Options.Options.Create(new AmazingMCP.Configuration.QueryUsagesOptions()));
    }

    async Task<IReadOnlyList<UsageMatch>> Act(
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