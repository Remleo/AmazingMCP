using AmazingMCP.Services.SymbolQuery;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

public class TypeNameHelperTests
{
    [TestCase("MyApp.Core.IRequestStream", "IRequestStream")]
    [TestCase("IRequestStream", "IRequestStream")]
    [TestCase("System.Collections.Generic.List<MyApp.Core.Animal>", "List")]
    [TestCase("MyApp.Persistance.IRepository<TKey, TValue>", "IRepository")]
    [TestCase("Foo.Bar`2", "Bar")]
    [TestCase("Foo.Bar<Baz.Qux>", "Bar")]
    [TestCase("Animal", "Animal")]
    public void GetSimpleName_VariousInputs_ReturnsSimpleNameWithoutNamespaceOrGenerics(string input, string expected)
    {
        // act
        var result = TypeNameHelper.GetSimpleName(input);

        // assert
        result.Should().Be(expected);
    }
}
