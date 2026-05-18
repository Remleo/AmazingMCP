using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.Wildcard;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests.SymbolQuery;

/// <summary>
/// Tests for TypeWildcardPatternBuilder.Build.
/// Covers all supported input formats and round-trip matching via WildcardPatternFactory.
/// </summary>
public class TypeWildcardPatternBuilderTests
{
    // ── Build output ────────────────────────────────────────────────────────

    [TestCase("Foo.Bar",                  "Foo.Bar",          "non-generic — unchanged")]
    // CLR backtick notation
    [TestCase("Foo.Bar`1",                "Foo.Bar<*>",       "CLR arity 1")]
    [TestCase("Foo.Bar`2",                "Foo.Bar<*, *>",    "CLR arity 2")]
    [TestCase("Foo.Bar`3",                "Foo.Bar<*, *, *>", "CLR arity 3")]
    [TestCase("Foo.Bar`0",                "Foo.Bar",          "CLR arity 0 — treated as non-generic")]
    // C# generic syntax — real param names replaced with *
    [TestCase("Foo.Bar<T>",               "Foo.Bar<*>",       "C# arity 1")]
    [TestCase("Foo.Bar<TModel, TValue>",  "Foo.Bar<*, *>",    "C# arity 2 — real param names ignored")]
    [TestCase("Foo.Bar<T1, T2, T3>",      "Foo.Bar<*, *, *>", "C# arity 3")]
    // nested generic args — arity counts top-level only
    [TestCase("Foo.Bar<Outer<T>, T2>",    "Foo.Bar<*, *>",    "nested generic arg — arity still 2")]
    [TestCase("Foo.Bar<Dict<K, V>, T2>",  "Foo.Bar<*, *>",    "deeply nested — arity still 2")]
    // already wildcard — unchanged
    [TestCase("Foo.Bar<*>",               "Foo.Bar<*>",       "already wildcard arity 1 — unchanged")]
    [TestCase("Foo.Bar<*, *>",            "Foo.Bar<*, *>",    "already wildcard arity 2 — unchanged")]
    [TestCase("Foo.Bar<*,*>",             "Foo.Bar<*, *>",    "no-space wildcards — comma at depth 0 → arity 2")]
    public void Convert_VariousInputs_ReturnsExpected(string input, string expected, string reason)
    {
        // act
        var result = TypeWildcardPatternBuilder.Build(input);

        // assert
        result.Should().Be(expected, reason);
    }

    // ── Round-trip: Build → CreateForTypeNames → IsMatch (should match) ───────────────

    [TestCase("Foo.Bar",               "Foo.Bar",              "non-generic exact")]
    [TestCase("Foo.Bar`1",             "Foo.Bar<TService>",    "arity 1 — * matches single arg")]
    [TestCase("Foo.Bar`2",             "Foo.Bar<TKey, TValue>","arity 2 — non-trailing * matches 'TKey', trailing * matches 'TValue'")]
    [TestCase("Foo.Bar<TModel>",       "Foo.Bar<MyModel>",     "C# input, concrete display")]
    [TestCase("Foo.Bar<TKey, TValue>", "Foo.Bar<string, int>", "C# input, concrete types")]
    [TestCase("Foo.Bar<Outer<T>, T2>", "Foo.Bar<X, Z>",     "nested generic — arity 2, first * matches 'X' (no delimiters), trailing * matches 'Z'")]
    public void Convert_ThenCompile_MatchesConcreteDisplayString(string input, string concreteDisplay, string reason)
    {
        // arrange
        var compiled = new WildcardPatternFactory().CreateForTypeNames(TypeWildcardPatternBuilder.Build(input));

        // act + assert
        compiled.IsMatch(concreteDisplay)
            .Should().BeTrue($"pattern for '{input}' should match '{concreteDisplay}' — {reason}");
    }

    // ── Round-trip: Build → CreateForTypeNames → IsMatch (should NOT match) ───────────

    [TestCase("Foo.Bar`1",  "Foo.Bar<TKey, TValue>", "arity 1 pattern: non-trailing * can't cross ','")]
    [TestCase("Foo.Bar`2",  "Foo.Bar<T>",            "arity 2 pattern: missing second arg")]
    [TestCase("Foo.Bar",    "Foo.Baz",               "different name")]
    [TestCase("Foo.Bar`2",  "Foo.Bar",               "arity 2 pattern: non-generic input")]
    public void Convert_ThenCompile_DoesNotMatchWrongDisplayString(string input, string wrongDisplay, string reason)
    {
        // arrange
        var compiled = new WildcardPatternFactory().CreateForTypeNames(TypeWildcardPatternBuilder.Build(input));

        // act + assert
        compiled.IsMatch(wrongDisplay)
            .Should().BeFalse($"pattern for '{input}' should NOT match '{wrongDisplay}' — {reason}");
    }
}
