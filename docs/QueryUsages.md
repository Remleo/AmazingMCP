# query_usages — Usage Search Across Solution

## Purpose

`query_usages` traverses the entire solution via Roslyn and finds all usages of a given type — method calls, property/field access, constructor calls, generic arguments, return types, parameter types, inheritance, `nameof`, `typeof`, and `is`/`as` checks.

Results are grouped by containing type and file, and rendered as annotated source code snippets inside a single `csharp` block per file.

## Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `typeName` | Yes | Fully qualified name of the **target type** to search for usages of |
| `predicate` | No | C# boolean expression to further filter results. Variable `x` is of type `QueryEntry` |
| `scanInclude` | No | Wildcard patterns restricting which **containing types** are scanned |
| `scanExclude` | No | Wildcard patterns for **containing types** to skip. Takes precedence over `scanInclude` |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

### typeName

The fully qualified name of the type to search for — must include the namespace.

- Example: `MyApp.Core.IRequestStream`
- For closed generics, all type arguments must also be fully qualified: `System.Collections.Generic.List<MyApp.Core.Animal>`
- For open generics, argument names must match the declaration: `MyApp.Persistance.IRepository<TKey, TValue>`

If no usages are found, the tool returns a detailed hint explaining how to find the correct full name using `query_symbol` or `code_lens`.

### predicate

A C# boolean expression evaluated as `Func<QueryEntry, bool>` with variable `x`. Compiled via `CSharpCompilation` into a dynamic assembly at runtime.

Supports `&&`, `||`, `()`. Allowed static calls: `Enumerable`, `String`, `Math`, `Convert`, `Enum`, `Type`. Instance method calls on any type are always allowed.

Forbidden: `new`, lambda expressions, anonymous methods, type declarations, static calls outside the whitelist.

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
| `MethodCall` | Instance method call with explicit receiver |
| `ConstructorCall` | `new MyType(...)` or `new(...)` |
| `PropertyRead` | Property getter access |
| `PropertyWrite` | Property setter or object initializer |
| `FieldRead` | Field read |
| `FieldWrite` | Field write or object initializer |
| `GenericArgument` | Type used as generic argument: `List<MyType>` |
| `GenericConstraint` | Type used in `where T : MyType` |
| `ReturnType` | Type used as return type of a method, property, or field |
| `Parameter` | Type used as a method or constructor parameter type |
| `Inheritance` | Type appears in base type list or interface list |
| `NameOf` | `nameof(MyType)` |
| `TypeOf` | `typeof(MyType)` |
| `IsOrAs` | `x is MyType` or `x as MyType` |

### scanInclude / scanExclude

Restrict which **containing types** are traversed. Does not affect what is searched — only where.

- `scanInclude: ["MyApp.Services.*"]` — only scan types in that namespace
- `scanExclude: ["*.Tests.*"]` — skip test types
- `scanExclude` takes precedence over `scanInclude`

## Output format

Results are grouped by `TypeName + FilePath`. Each group is rendered as:

```
## MyApp.Services.AnimalService

file: C:\...\AnimalService.cs

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
        _sut = new AnimalService(
            _repository,
            _logger);
```

### Line annotations

- `// line N +1` — single-line section
- `// lines N +K` — multi-line section (K = line count)
- `// ...` — cut separator between non-adjacent sections; indented to match the surrounding code

### Section resolution

For each matched usage node, `SectionResolver` walks up the AST:

- **`BlockSyntax` encountered first:** measures the parent node's total span.
  - If ≤ 8 lines → section is the **full parent** (e.g. `catch (...) { ... }`, `if (...) { ... }`)
  - If > 8 lines → section falls back to the **usage node itself**
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
| `IfStatementSyntax` | Condition only — when usage is in the condition, not the body |
| `WhileStatementSyntax` | Condition only — when usage is in the condition, not the body |
| `ForStatementSyntax` | Condition + initializer (not body) |
| `ParameterSyntax` in primary constructor | Entire `ParameterListSyntax` |
| `PropertyDeclarationSyntax` | Type declaration line only |
| `MethodDeclarationSyntax` / `ConstructorDeclarationSyntax` | Signature lines (from attributes to opening brace) |

### Truncation

Output is truncated at `--QueryUsages:QueryMatchLimit` (default 200 matches) with a note:
```
> Too many results (200+ matches). Output is truncated. Narrow your query using a more specific predicate or add scanInclude/scanExclude.
```

## Implementation

```
QueryUsagesService
├── IUsageProvider.QueryAsync()
│   └── UsageProvider
│       ├── UsagePredicateCompiler.CompileAsync()     — compiles predicate via CSharpCompilation
│       │   └── PredicateSafetyValidator              — validates predicate before compilation
│       ├── IWorkspaceProvider.GetSolutionAsync()
│       ├── Per compilation → per syntax tree:
│       │   └── UsageSyntaxWalker (CSharpSyntaxWalker)
│       │       ├── Scope stack: tracks current type + method + definition range
│       │       ├── TryEnterType() — applies scanInclude/scanExclude filters
│       │       ├── VisitMethodDeclaration/ConstructorDeclaration/PropertyDeclaration — tracks method scope
│       │       └── DefaultVisit() — QueryEntryFactory.TryCreate() per node → predicate → SectionResolver.Resolve()
│       │           └── QueryEntryFactory — creates QueryEntry for each usage kind
│       └── IInheritanceUsageProvider.FindMatches()  — finds Inheritance usages (base type lists)
└── IUsageResultFormatter.Format()
    └── UsageResultFormatter
        ├── Groups by (TypeName, FilePath)
        ├── MergeRanges() — merges overlapping section + method definition ranges
        └── AppendCodeLines() — reads source file lines, formats with // line N +K annotations
```

## Typical agent workflow

```
1. query_symbol("IAnimalRepository")                          → find the full type name
2. query_usages("MyApp.Core.IAnimalRepository")               → find all usages
3. query_usages("MyApp.Core.IAnimalRepository",
     predicate: "x.Kind == UsageKind.ConstructorCall")        → only instantiations
4. query_usages("MyApp.Core.IAnimalRepository",
     scanInclude: ["MyApp.Services.*"])                       → only in services
```
