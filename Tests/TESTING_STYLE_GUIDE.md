# Test Style Guide — methodology, style & rules (AI guide)

This document summarizes the **testing methodologies, patterns, conventions, and frameworks** used in the existing tests in this repository.

Use this as a **strict context/rule set** for generating new tests that match the same style.

---

## 1) Stack / frameworks / libraries

### Test framework
- **NUnit**
  - `[Test]`, `[SetUp]`, and `[TestCase(...)]` attributes
  - Plain classes as fixtures (no base test class)

### Mocking
- **NSubstitute**
  - `Substitute.For<T>()`
  - Stubbing returns: `.Returns(...)`, `.ReturnsForAnyArgs(...)`
  - Stubbing async dependencies: `...Async(...).Returns(...)`
  - Capturing side effects with `.When(...).Do(call => { ... })`
  - Throwing exceptions: `.ThrowsForAnyArgs(...)`
  - Verifying calls: `.Received(n)`

### Assertions
- **FluentAssertions**
  - Primary assertion style: `.Should().BeEquivalentTo(...)`
  - `.Should().BeEmpty()` for “not created / nothing added” cases
  - `.Should().NotBeNull()` / `.Should().Be(...)` for simple checks
  - Explicit nulls in anonymous shapes via `(object?) null`

---

## 2) Overall methodology

### 2.1 AAA structure (Arrange / Act / Assert)
- Tests follow Arrange/Act/Assert (AAA).
- AAA phases are **explicitly labeled with comments**:
  - `// arrange`
  - `// act`
  - `// assert`
- For multi-step scenarios, additional phases are labeled:
  - `// act2: ...`
  - `// assert 2`

### 2.2 Behavior-oriented tests
- Tests validate **observable behavior**:
  - state changes on domain objects / entities
  - produced outputs (e.g., notifications/update units)
  - downstream side effects (e.g., “publisher called”, “storage updated”)
- Prefer interacting with the SUT via its **public API** and asserting the result.

### 2.3 “Only change what must change”
A recurring theme:
- If the input equals the default/baseline state, **don’t create extra objects/graphs**.
- If a value is disabled/invalid, it’s treated as **removal** or **fallback**.

Keep new tests focused on these invariants:
- expected changes happen
- unexpected changes do *not* happen

---

## 3) Test class structure conventions

### 3.1 Private fields
- Test classes define fields for:
  - `_sut` (System Under Test)
  - key input/output objects used across tests
  - dependency mocks (interfaces)

Naming examples:
- `_sut`
- `_dependency` / `_service` / `_repository`

### 3.2 `[SetUp]` builds a realistic base state
- Dependencies are mocked.
- `_sut` is constructed once per test (in `[SetUp]`).
- Base objects are created with minimal required identifiers/fields.

### 3.3 Constants for readability
- Common ids/keys are declared as `const` (or `static readonly` for complex types).

**FixtureId convention (domain):**
> In this codebase, fixture ids are always strings in the format `\d+:\d+` (e.g. `"2:123"`).
>
> When writing tests, prefer using realistic fixture ids in this format unless the test explicitly targets invalid input handling.

**StringId convention (domain):**
> In this codebase, string ids used for translations are strings in the format `"2:<number>"` (e.g. `"2:123"`, `"2:124"`, ...).
>
> When writing tests that operate on translation string ids, use this format.

---

## 4) Reusable helpers inside the test class

To reduce duplication, tests often define private helpers at the bottom:
- `Act()` method(s) that execute the common “act” flow and return results.
- `CreateXxx()` / `HasInitialXxx(...)` methods to create/populate base graphs.
- `CreateMapper()` helper for a minimal mapper substitute.

Rules:
- Helpers should keep the test body short.
- Helpers must not hide important behavior; core scenario should still be readable in the test.

---

## 5) Assertion style rules (FluentAssertions)

### 5.1 Prefer `BeEquivalentTo` with anonymous objects
Instead of asserting every property directly, tests use:
- `actual.Should().BeEquivalentTo(new { ... })`

This produces **focused assertions** (only the fields that matter for the behavior).

**Rule (readability):**
> Do not write multiple property assertions like:
> `result.Should().NotBeNull();`
> `result.FixtureId.Should().Be(fixtureId);`
> `result.EntityType.Should().Be(entityType);`
>
> Instead, replace them with a single equivalent check:
> `result.Should().BeEquivalentTo(new { FixtureId = fixtureId, EntityType = entityType });`

### 5.2 Use shape-based assertions to minimize coupling
- Assert only properties relevant to the rule under test.
- Avoid asserting entire graphs when only a few fields matter.

### 5.3 Explicit `null` is expressed as `(object?) null`
To avoid type ambiguity in anonymous objects:
- `SomeProperty = (object?) null`

### 5.4 Collection equivalence and emptiness
- For collections, prefer equivalence:
  - `.Should().BeEquivalentTo(new[] { new { ... } })`
- For “should not exist / should not be created”, prefer:
  - `.Should().BeEmpty()`

**Rule (readability):**
> When asserting that a filtered collection contains exactly one (or a known small set of) element(s), prefer asserting the whole collection with `BeEquivalentTo(...)`:
>
> `requests.OfType<SomeType>().Should().BeEquivalentTo(new[] { new { ... } });`
>
> instead of chaining `ContainSingle().Which.Should().BeEquivalentTo(...)`.
>
> This simultaneously verifies **count** and **shape** and reads as a single expectation.

---

## 6) Mocking style rules (NSubstitute)

### 6.1 Basic substitution
- Always use `Substitute.For<T>()`.

### 6.2 Async stubbing
- Stub `Task`/`Task<T>` using `.Returns(...)`.
- Match cancellation tokens using `Arg.Any<CancellationToken>()` unless the token value is the focus of the test.

**Rule (readability):**
> When stubbing async methods (`Task<T>`) with a constant result, prefer returning the value directly.
>
> For example, instead of:
> `someAsyncCall.ReturnsForAnyArgs(Task.FromResult<MyType?>(value));`
>
> write:
> `someAsyncCall.ReturnsForAnyArgs(value);`
>
> For null results, keep the type explicit:
> `someAsyncCall.ReturnsForAnyArgs((MyType?) null);`

### 6.3 Flexible matching to reduce noise
- Use `ReturnsForAnyArgs(...)` when argument values aren’t important for the behavior.

**Rule (clean code):**
> Prefer `ReturnsForAnyArgs(...)` over `Returns(...)` whenever a test does not need to differentiate calls by arguments.
>
> This keeps Arrange blocks smaller and reduces coupling to parameter values (especially `CancellationToken`).

### 6.4 Side effects and minimal mapping
- Prefer `.When(...).Do(...)` to inject controlled behavior (e.g., minimal mapping).
- Avoid introducing heavy real configurations (like full AutoMapper profiles) unless strictly necessary.

### 6.5 Verifying effects via call assertions
When the behavior is “a dependency is called”, assert using:
- `await dependency.Received(1).SomeAsyncMethod(...)`

### 6.6 Stubbing exceptions
- Prefer `Throws(...)` / `ThrowsForAnyArgs(...)` for **sync** methods.
- For **async** methods returning `Task` / `Task<T>` prefer NSubstitute async helpers:
  - `ThrowsAsync(...)`
  - `ThrowsAsyncForAnyArgs(...)`

Examples:
```csharp
dependency.SomeAsync(default)
    .ThrowsAsyncForAnyArgs(new InvalidOperationException("boom"));

dependency.SomeAsyncWithResult(default)
    .ThrowsAsyncForAnyArgs(new InvalidOperationException("boom"));
```

Fallback:
> If `ThrowsAsync...` is not available for a specific call shape, stub via `Returns(... => Task.FromException(...))`.

---

## 7) Async and CancellationToken conventions

- Async test methods use `public async Task ...`.
- Use `CancellationToken.None` by default.
- Cancellation-specific tests:
  - create a `CancellationTokenSource`
  - configure the dependency to throw `OperationCanceledException(ct)`
  - cancel before calling SUT
  - assert using FluentAssertions exception assertions (see section 13)

### 7.1 Negative call assertions ("DidNotReceive") in async scenarios
When the SUT work is triggered asynchronously (events, background tasks, fire-and-forget):
- Use `WaitAssertion(...)` for **positive** expectations (e.g. `Received(1)`), waiting until the work has definitely happened.
- **Do not** put `DidNotReceive...` inside `WaitAssertion(...)` — it may pass *before* the asynchronous work even started.
- For negative expectations, first **stabilize** (e.g. `await Task.Delay(...)` / a shared `DelayShort()` helper), then assert `DidNotReceive...`.

**Async methods note (CS4014):**
> For async methods returning `Task`/`Task<T>`, prefer writing negative assertions as an **awaited** call:
>
> `await substitute.DidNotReceive().SomeAsyncMethod(...);`
>
> This removes CS4014 noise and keeps the intent explicit.
>
> If an awaited negative assertion becomes impractical (e.g. too broad / hard to express), fall back to inspecting calls (e.g. `substitute.ReceivedCalls()`), but only after stabilization.

---

## 8) Parameterized tests

- Use `[TestCase(...)]` when the same behavior should be verified for multiple input variants.
- A `switch (testCase)` in Arrange/Act is an acceptable pattern.

Rule of thumb:
> Use `[TestCase]` for “different triggers, same expectation”.

### 8.1 When cases are very similar (minor Arrange/Assert variations)
If scenarios are almost identical and differ only in small **Arrange** nuances and/or small **Assert** nuances, it’s acceptable to parameterize the test with:
- `string arrangeCase`
- `string assertCase`

And use small `switch` blocks with **hardcoded human-readable** case values, e.g.:
- `"with_prices"`
- `"price_be_empty"`

Rules:
- Keep the switches small and local to the test (don’t hide the scenario).
- Use `default:` to throw (e.g. `ArgumentOutOfRangeException`) so unknown case values fail fast.
- Prefer this when you want to share the same Act flow and most of the test body.

Conceptual example:
```csharp
[TestCase("with_prices", "has_prices")]
[TestCase("no_prices", "price_be_empty")]
public async Task Method_Scenario_Expected(string arrangeCase, string assertCase)
{
    // arrange
    switch (arrangeCase)
    {
        case "with_prices":
            // setup inputs with prices
            break;

        case "no_prices":
            // setup inputs without prices
            break;

        default:
            throw new ArgumentOutOfRangeException(nameof(arrangeCase), arrangeCase, "Unknown arrangeCase");
    }

    // act
    var result = await Act();

    // assert
    switch (assertCase)
    {
        case "has_prices":
            result.Prices.Should().NotBeEmpty();
            break;

        case "price_be_empty":
            result.Prices.Should().BeEmpty();
            break;

        default:
            throw new ArgumentOutOfRangeException(nameof(assertCase), assertCase, "Unknown assertCase");
    }
}
```

---

## 9) Naming conventions

### 9.1 Test class naming
- `{SutTypeName}Tests`

### 9.2 Test method naming
Pattern:
- `{MethodUnderTest}_{Scenario}_{ExpectedResult}`

Rules:
- Use underscores.
- Names may be long; encode the key business rule clearly.

### 9.3 Local variables
- Use descriptive names.
- Prefer `const` for ids.

---

## 10) Comments and readability

- Comments are sparse and purposeful.
- Primary usage is to label AAA sections:
  - `// arrange`
  - `// act`
  - `// assert`
- Use short clarifying comments only when the intent isn’t obvious.

---

## 11) Do / Don’t rules (AI-agent rules)

### Do
- Use **NUnit + NSubstitute + FluentAssertions**.
- Use explicit `// arrange`, `// act`, `// assert` comments.
- Prefer `BeEquivalentTo` with minimal anonymous shapes.
- Add “noise” inputs when validating filtering/selection logic.
- Assert “nothing created/changed” explicitly (e.g., `.BeEmpty()`).
- Keep tests small and single-purpose.
- Use or introduce helpers to reduce code duplication.

### Don’t
- Don’t over-assert unrelated properties.
- Don’t rely on ordering unless it’s part of the contract.
- Don’t introduce heavyweight real configurations if a substitute is enough.

---

## 12) Minimal templates (conceptual)

### 12.1 Standard unit test
- Arrange: build SUT + mocks + inputs
- `// act`: call the SUT
- `// assert`: `BeEquivalentTo` or `.Received(...)`

### 12.2 Integration-ish unit test (in-memory persistence/change tracking)
- Arrange: create a context, add/attach entities, mutate tracked fields
- `// act`: invoke generator/service
- `// assert`: assert produced output + flags, with shape-based equivalence

---

## 13) Exception testing

### 13.1 Exception assertions (preferred)
Prefer **FluentAssertions** for exception checks.

**Primary style (matches this repo’s conventions):**
- **Sync** (method returns `void`):
  - `_sut.Invoking(sut => sut.Do())`
    `.Should().Throw<SomeException>();`

- **Async** (method returns `Task`):
  - `await _sut.Invoking(sut => sut.DoAsync())`
    `.Should().ThrowAsync<SomeException>();`

Notes:
- This style keeps the `act` expression close to `_sut` and reads well for fluent chains.
- Use this especially when the SUT call has multiple parameters or spans multiple lines.

---

## 14) Accessing private members in tests

When a test needs to call a `private` or `internal` method (or access a field), use the `[UnsafeAccessor]` attribute (.NET 8+) — **not** reflection.

Define a `static extern` accessor method in the test class (or a shared helper) with the matching signature:

```csharp
using System.Runtime.CompilerServices;

// Access a private instance method
[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CheckProcessingLatency")]
static extern bool CheckProcessingLatency(EntityHealthMonitor target, /* original params */);

// Access a private static method
[UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SomeStaticMethod")]
static extern int SomeStaticMethod(EntityHealthMonitor target, int arg);

// Access a private field
[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_myField")]
static extern ref int GetMyField(EntityHealthMonitor target);
```

Rules:
- Prefer `[UnsafeAccessor]` over reflection (`BindingFlags.NonPublic`, `Invoke`, etc.) — it's compile-time safe and has zero runtime overhead.
- Do **not** make members `public` solely for testability.
- The first parameter of the accessor is always the target instance (even for fields).
- The `Name` must match the private member name exactly.
