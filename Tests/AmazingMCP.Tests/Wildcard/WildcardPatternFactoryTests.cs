using AmazingMCP.Services;
using AmazingMCP.Services.Wildcard;
using FluentAssertions;
using NUnit.Framework;

namespace AmazingMCP.Tests;

/// <summary>
/// Tests for WildcardPatternFactory.CreateForTypeNames and CreateGlob / WildcardPattern.IsMatch.
///
/// CreateForTypeNames semantics (segment-aware):
///   - Leading/trailing '*' → matches any sequence including delimiters.
///   - Middle '*'           → does NOT cross ',', ' ', '&lt;', '&gt;'.
///
/// CreateGlob semantics:
///   - '*' always matches any sequence including all delimiters.
/// </summary>
public class WildcardPatternFactoryTests
{
    // ── CreateForTypeNames ────────────────────────────────────────────────────

    [TestCase("Foo.Bar",         "Foo.Bar",        true,  "exact match")]
    [TestCase("Foo.Bar",         "foo.bar",        true,  "case-insensitive")]
    [TestCase("Foo.Bar",         "Foo.Baz",        false, "different name")]
    // trailing '*' — matches everything
    [TestCase("Foo.Bar",         "*",              true,  "single trailing * matches all")]
    [TestCase("Foo.Bar",         "Foo.*",          true,  "trailing * matches 'Bar'")]
    [TestCase("Foo.Bar.Baz",     "Foo.*",          true,  "trailing * matches 'Bar.Baz'")]
    [TestCase("Foo.Bar<T>",      "Foo.*",          true,  "trailing * matches 'Bar<T>'")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.*",          true,  "trailing * matches 'Bar<T1, T2>'")]
    // leading '*' — matches everything before
    [TestCase("Foo.Bar<T>",      "*Bar*",          true,  "leading * matches 'Foo.', trailing * matches '<T>'")]
    [TestCase("Foo.Bar<T1, T2>", "*Bar*",          true,  "leading * matches 'Foo.', trailing * matches '<T1, T2>'")]
    [TestCase("FooBar<T>",       "*Bar*",          true,  "leading * matches 'Foo', trailing * matches '<T>'")]
    [TestCase("FooBar",          "*Bar*",          true,  "no delimiters")]
    [TestCase("Foo.Bar",         "*Baz*",          false, "no match")]
    [TestCase("Foo.Bar",         "*.Bar",          true,  "leading * matches 'Foo'")]
    [TestCase("Foo.Bar",         "*Foo*",          true,  "leading * matches '', trailing * matches '.Bar'")]
    // middle '*' — stops at ',', ' ', '<', '>'
    [TestCase("Foo.Bar<T>",      "Foo.Bar<*>",     true,  "middle * matches 'T'")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.Bar<*>",     false, "middle * can't cross ',' — arity mismatch")]
    [TestCase("Foo.Bar<T1,T2>",  "Foo.Bar<*>",     false, "middle * can't cross ',' — no-space variant")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.Bar<*, *>",  true,  "middle * matches 'T1', trailing * matches 'T2'")]
    [TestCase("Foo.Bar<T1,T2>",  "Foo.Bar<*,*>",   true,  "no-space variant")]
    [TestCase("Foo.Bar<T>",      "Foo.Bar<*, *>",  false, "middle * stops at '>' — can't reach ', *'")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.Bar<*,*>",   false, "pattern no-space, input has space")]
    [TestCase("Foo.Bar<T1,T2>",  "Foo.Bar<*, *>",  false, "pattern has space, input has none")]
    [TestCase("Foo.Bar<T>",      "Foo.*<*>",       true,  "middle * matches 'Bar', second middle * matches 'T'")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.*<*, *>",    true,  "middle * matches 'Bar', second matches 'T1', trailing matches 'T2'")]
    [TestCase("Foo.Bar<T>",      "Foo.Bar",        false, "non-generic pattern vs generic type")]
    // leading vs middle vs trailing — explicit distinction
    // leading: first wildcard, nothing before it
    [TestCase("Foo.Bar<T1, T2>", "*<T1, T2>",      true,  "leading * matches 'Foo.Bar' including '.'")]
    [TestCase("Foo.Bar<T1, T2>", "*Bar<T1, T2>",   true,  "leading * matches 'Foo.'")]
    // trailing: last wildcard, nothing after it
    [TestCase("Foo.Bar<T1, T2>", "Foo.Bar<T1,*",   true,  "trailing * matches ' T2>' — comma matches, * swallows rest")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.Bar<T1, *",  true,  "trailing * matches 'T2>'")]
    [TestCase("Foo.Bar<T>",      "Foo.Bar<*",       true,  "trailing * matches 'T>'")]
    // middle: wildcard surrounded by literal text on both sides
    [TestCase("Foo.Bar<T>",      "Foo.*<T>",        true,  "middle * matches 'Bar' — no delimiters in 'Bar'")]
    [TestCase("Foo.Baz<T>",      "Foo.*<T>",        true,  "middle * matches 'Baz'")]
    [TestCase("Foo.Bar.Baz<T>",  "Foo.*<T>",        true,  "middle * matches 'Bar.Baz' — dots allowed")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.*<T1, T2>",   true,  "middle * matches 'Bar'")]
    [TestCase("Foo.Bar<T1, T2>", "Foo.*<T1,T2>",    false, "middle * matches 'Bar' but literal '<T1,T2>' doesn't match '<T1, T2>'")]
    public void CreateForTypeNames_ThenIsMatch_ReturnsExpected(string input, string pattern, bool expected, string reason)
    {
        // arrange
        var compiled = new WildcardPatternFactory().CreateForTypeNames(pattern);

        // act
        var result = compiled.IsMatch(input);

        // assert
        result.Should().Be(expected, reason);
    }

    // ── CreateGlob ────────────────────────────────────────────────────────────

    [TestCase("Foo.Bar",                                                       "*",                     true,  "matches all")]
    [TestCase("Foo.Bar",                                                       "*Bar*",                 true,  "contains match")]
    [TestCase("Foo.Bar<T>",                                                    "*Bar*",                 true,  "* crosses '<'")]
    [TestCase("Foo.Bar<T1, T2>",                                               "*Bar*",                 true,  "* crosses '<', ','")]
    [TestCase("Foo.Bar<T1, T2>",                                               "*Foo.Bar<*>",           true,  "* crosses all delimiters")]
    [TestCase("async Task<FixtureSubscription?> GetFixtureSubscriptionAsync(string id)", "*FixtureSubscription*", true,  "* crosses '<' in return type")]
    [TestCase("void UpdateFixtureSubscription(IMessage msg)",                  "*FixtureSubscription*", true,  "plain method name match")]
    [TestCase("async Task<string> GetNameAsync(string id)",                    "*FixtureSubscription*", false, "no match")]
    [TestCase("Foo.Bar",                                                       "Foo.*",                 true,  "trailing * matches rest")]
    [TestCase("Foo.Bar.Baz",                                                   "Foo.*",                 true,  "trailing * matches 'Bar.Baz'")]
    [TestCase("Foo.Bar",                                                       "Foo.Baz",               false, "exact mismatch")]
    [TestCase("Foo.Bar",                                                       "foo.bar",               true,  "case-insensitive")]
    public void CreateGlob_ThenIsMatch_ReturnsExpected(string input, string pattern, bool expected, string reason)
    {
        // arrange
        var compiled = new WildcardPatternFactory().CreateGlob(pattern);

        // act
        var result = compiled.IsMatch(input);

        // assert
        result.Should().Be(expected, reason);
    }
}
