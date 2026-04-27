# query_usages — Usage Search Across Solution

## Purpose

`query_usages` traverses the entire solution via Roslyn and finds all usages of a given type —
method calls, property/field access, constructor calls, generic arguments, return types, and parameter types.

Results are grouped by containing type and file, and rendered as annotated source code snippets
inside a single `csharp` block per file.

## Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `typePattern` | Yes | Wildcard pattern matched against the full name of the **target type** involved in each usage |
| `predicate` | No | C# boolean expression to further filter results. Variable `x` is of type `QueryEntry` |
| `scanInclude` | No | Wildcard patterns restricting which **containing types** are scanned |
| `scanExclude` | No | Wildcard patterns for **containing types** to skip. Takes precedence over `scanInclude` |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

### typePattern

Matched against `QueryEntry.TypeName` — the full name of the type involved in the usage.

- Prefer the fully qualified name including namespace to avoid false positives: `MyApp.Core.IRequestStream`
- Supports `*` wildcard: `MyApp.Services.*`, `*IRequestStream*`
- If the pattern contains no `*` and no `.` it is automatically wrapped as `*pattern*`

### predicate

A C# boolean expression evaluated as `Func<QueryEntry, bool>` with variable `x`.

Supports `&&`, `||`, `()` for complex conditions. Allowed static calls: `Enumerable`, `String`, `Math`, `Convert`, `Enum`, `Type`.

Forbidden: `new`, lambda expressions, type declarations, static calls outside the whitelist.

**`QueryEntry` fields:**

| Field | Type | Populated for |
|---|---|---|
| `TypeName` | `string` | All kinds — full name of the target type |
| `Kind` | `UsageKind` | All kinds |
| `MethodName` | `string?` | `MethodCall`, `ConstructorCall` (type name) |
| `ArgumentTypes` | `IReadOnlyList<string>?` | `MethodCall`, `ConstructorCall` |
| `PropertyName` | `string?` | `PropertyRead`, `PropertyWrite` |
| `FieldName` | `string?` | `FieldRead`, `FieldWrite` |

**`UsageKind` values:**

| Value | Description |
|---|---|
| `MethodCall` | Instance method call with explicit receiver (`_repo.FindById(...)`) |
| `ConstructorCall` | `new MyType(...)` or `new(...)` |
| `PropertyRead` | Property getter access |
| `PropertyWrite` | Property setter or object initializer |
| `FieldRead` | Field read |
| `FieldWrite` | Field write or object initializer |
| `TypeAsGenericArgument` | Type used as generic argument: `List<MyType>`, `IHandler<MyType, int>` |
| `TypeAsGenericConstraint` | Type used in `where T : MyType` |
| `TypeAsReturnType` | Type used as return type of a method, property, or field declaration |
| `TypeAsParameter` | Type used as a method or constructor parameter type |

**Note:** Self-references (accessing own fields/methods without explicit receiver) are excluded.

### scanInclude

Restricts which **containing types** are traversed. Does not affect what is searched — only where.

- Leave `null` to scan the entire solution
- Supports `*` wildcard: `["MyApp.Services.*", "MyApp.Core.*"]`

### scanExclude

Excludes specific **containing types** from traversal. Takes precedence over `scanInclude`.

- Leave `null` to exclude nothing
- Supports `*` wildcard: `["*.Tests.*", "MyApp.Generated.*"]`

## Output format

Results are grouped by `TypeName + FilePath`. Each group is rendered as a single `csharp` block:

```
## My.Namespace.MyClass  `path/to/File.cs`

```csharp
    // line 10 +1
    public void MyMethod(Animal animal)
    {
    // ...
        // lines 25 +2
        var result = _repo.FindById(animal.Id);
        return result;
    // ...
        // lines 44 +7
        _sut = new RefreshRequestStream(
            _betContentSearcher,
            ...
            _logger);
```

### Line annotations

- `// line N +1` — single-line section
- `// lines N +K` — multi-line section (K = line count)
- `// ...` — cut separator between non-adjacent sections; indented to match the surrounding code

### Method definition headers

When a usage is inside a method body, the method signature is shown before the first section from that method:

```csharp
    // lines 30 +47
    public void SetUp()
    {
    // ...
        // line 37 +1
        _logger = Substitute.For<ILogger<RefreshRequestStream>>();
```

The annotation `// lines N +K` on the definition header reflects the **full method span** (signature + body), so the reader knows the total extent and can read the relevant lines directly. The definition is shown **once per method per file**, even if the method has multiple non-adjacent matches.

### Section resolution

For each matched usage node, `SectionResolver` walks up the AST:

- **`BlockSyntax` encountered first:** the parent node's total span (block + keyword line) is measured.
  - If ≤ 8 lines → section is the **full parent** (e.g. `catch (...) { ... }`, `if (...) { ... }`)
  - If > 8 lines → section falls back to the **usage node itself** (no surrounding context)
- **Other qualifying ancestor found first:**

| Ancestor | Section span |
|---|---|
| `InvocationExpressionSyntax` | Full call including arguments |
| `ObjectCreationExpressionSyntax` | Full `new` expression (including initializer) |
| `AssignmentExpressionSyntax` | Full assignment (unless inside object initializer — skipped) |
| `LocalDeclarationStatementSyntax` | Full `var x = ...` statement |
| `FieldDeclarationSyntax` | Full field declaration |
| `ReturnStatementSyntax` | Full `return ...` |
| `ThrowStatementSyntax` / `ThrowExpressionSyntax` | Full `throw ...` |
| `IfStatementSyntax` | Condition only — when usage is **in the condition**, not the body |
| `WhileStatementSyntax` | Condition only — when usage is **in the condition**, not the body |
| `ForStatementSyntax` | Condition + initializer (not body) |
| `ParameterSyntax` in primary constructor | Entire `ParameterListSyntax` |
| `PropertyDeclarationSyntax` | Type declaration line only |

**Note on `TypeName`:** for method calls and property/field access, `TypeName` reflects the **receiver's actual type** (e.g. `ILogger<RefreshRequestStream>`), not the declaring type. This means searching for `*ILogger*` finds calls like `logger.LogInformation(...)` even when `LogInformation` is an extension method declared on a different type.

### Predicate safety (`PredicateSafetyValidator`)

Before compilation, the predicate expression is parsed and validated:
- Forbidden: `new`, lambda expressions, anonymous methods, type declarations
- Static calls allowed only from: `Enumerable`, `String`, `Math`, `Convert`, `Enum`, `Type`
- Instance method calls on any type are always allowed

The predicate is compiled via `CSharpCompilation` into a dynamic assembly and invoked per entry.

### Output formatting (`UsageResultFormatter`)

1. All section ranges for a file are merged globally (sorted by start line, overlapping ranges joined)
2. For each merged block, methods whose definitions are not fully contained in the block are shown as headers
3. Each method definition is shown at most once per file
4. `// ...` separators use the indentation of the following code block
5. Leading attributes (`[...]`) and XML doc comments (`///`) are stripped from method definition headers
