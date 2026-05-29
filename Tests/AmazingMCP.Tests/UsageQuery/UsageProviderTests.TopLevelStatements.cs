using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.UsageQuery;

/// <summary>
/// Regression: usages inside top-level statements (no enclosing type declaration in source)
/// must be discovered. Roslyn synthesizes a <c>Program</c> type with a <c>&lt;Main&gt;$</c> method
/// to host the statements; the walker must recognise that synthesized scope.
/// </summary>
public class UsageProviderTestsTopLevelStatements : UsageProviderTestsBase
{
    [Test]
    public async Task QueryAsync_ConstructorCallInTopLevelStatements_IsFound()
    {
        // act — Animal is instantiated only in TestProject.Host\Program.cs (top-level statements)
        var matches = await Act("TestProject.Core.Models.Animal");

        // assert
        matches.Should().Contain(m =>
            m.Scope.FilePath.EndsWith("Program.cs", System.StringComparison.OrdinalIgnoreCase)
            && m.Entry.Kind == AmazingMCP.Models.UsageQuery.UsageKind.ConstructorCall);
    }

    [Test]
    public async Task QueryAsync_PropertyAccessInTopLevelStatements_IsFound()
    {
        // act — Program.cs reads animal.Name and animal.Kind after construction
        var matches = await Act("TestProject.Core.Models.Animal");

        // assert
        matches.Should().Contain(m =>
            m.Scope.FilePath.EndsWith("Program.cs", System.StringComparison.OrdinalIgnoreCase)
            && m.Entry.Kind == AmazingMCP.Models.UsageQuery.UsageKind.PropertyRead);
    }
}
