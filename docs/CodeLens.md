# code_lens — Type Resolution for a Code Span

## Purpose

`code_lens` analyzes a line range inside a `.cs` file via Roslyn semantic analysis and resolves
all type names to their fully qualified forms — with namespace, generic arguments, and nullability.

The primary use case is understanding what types are actually flowing through a block of code
without having to navigate the full solution manually. It answers questions like:
- What is the exact type of this `var`?
- What does this method return?
- What are the full types of these arguments?
- What interfaces does this class implement?

## Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `filePath` | Yes | Absolute or relative path to the `.cs` file to analyze |
| `startLine` | Yes | First line of the range to analyze (1-based, inclusive) |
| `endLine` | Yes | Last line of the range to analyze (1-based, inclusive) |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

## What is collected

The tool walks all syntax nodes within the requested span and classifies them into five categories:

| Category | What triggers it | What is resolved |
|---|---|---|
| **Variables** | `var` / typed local variable declarations | Full type name of the variable |
| **Calls** | Instance and static method calls | Return type + non-trivial argument types |
| **Extensions** | Extension method calls | Receiver type + return type + non-trivial argument types |
| **Constructors** | `new T(...)` and `new(...)` expressions | Constructed type + non-trivial argument types |
| **Definitions** | Method, constructor, class, interface, record, struct declarations | Parameter types + return type (methods); base types and interfaces listed in syntax (types) |

Nested calls are both collected: `foo(bar(x))` produces entries for both `foo` and `bar`.

## Output format

Results are grouped into sections. Each section is only shown if it has entries.

```
## Variables
var animal: TestProject.Core.Models.Animal
var filtered: List<TestProject.Core.Models.Animal>

## Calls
.FindById() → TestProject.Core.Models.Animal
.Save()  |  args: [0] TestProject.Core.Models.Animal

## Extensions
.Where(,) on IReadOnlyList<TestProject.Core.Models.Animal> → IEnumerable<TestProject.Core.Models.Animal>  |  args: [0] Func<TestProject.Core.Models.Animal, Boolean>
.ToList() on IEnumerable<TestProject.Core.Models.Animal> → List<TestProject.Core.Models.Animal>

## Constructors
new TestProject.App.Services.OrderService(,)  |  args: [0] TestProject.Core.Persistence.IAnimalRepository, [1] TestProject.Core.Services.INotificationService

## Definitions
def new AnimalService(,)  |  params: [0] TestProject.Core.Persistence.IAnimalRepository, [1] TestProject.Core.Services.INotificationService
def GetById() → TestProject.Core.Models.Animal
def TestProject.App.Services.AnimalService : TestProject.Core.Services.IAnimalService
```

### Argument placeholder

Method names are shown with a comma-placeholder indicating arity — no argument values, just shape:

| Arg count | Placeholder |
|---|---|
| 0 or 1 | `()` |
| 2 | `(,)` |
| 3 | `(,,)` |

### Argument detail

Non-trivial argument types are listed after `|  args:` with 0-based indices:

```
.Process(,) → Task<TestProject.Core.Models.Result>  |  args: [0] TestProject.Core.Models.Order
```

### System namespace trimming

Types whose namespace starts with `System.` have the namespace stripped — only the short name
and generic arguments are shown:

| Full name | Displayed as |
|---|---|
| `System.Collections.Generic.IEnumerable<MyApp.Core.Item>` | `IEnumerable<MyApp.Core.Item>` |
| `System.Collections.Generic.List<MyApp.Core.Item>` | `List<MyApp.Core.Item>` |
| `System.Threading.Tasks.Task<MyApp.Core.Result>` | `Task<MyApp.Core.Result>` |
| `System.Func<MyApp.Core.Item, System.Boolean>` | `Func<MyApp.Core.Item, Boolean>` |

Non-`System.*` namespaces (project types, NuGet types) are always shown in full.

### Trivial type filtering

The following types are considered trivial and are **never shown** in the output:

- Primitives: `string`, `bool`, `int`, `uint`, `long`, `ulong`, `short`, `ushort`, `byte`, `sbyte`, `float`, `double`, `decimal`, `char`, `object`, `void`
- `System.Threading.CancellationToken`
- `System.Threading.Tasks.Task` and `System.Threading.Tasks.ValueTask` (without generic argument)
- `Nullable<T>` where `T` is itself trivial (e.g. `int?`, `bool?`)
- `Task<T>` and `ValueTask<T>` where `T` is trivial (e.g. `Task<int>`)

`Task<MyApp.Core.Result>` is **not** trivial and will be shown.

### Deduplication

Each category deduplicates by a typed record key:

| Category | Deduplication key |
|---|---|
| Variables | `(Name, TypeFullName)` |
| Calls | `(MethodName)` — only by name, overloads collapse into one entry |
| Extensions | `(MethodName)` |
| Constructors | `(TypeFullName)` |
| Definitions | `(Name, Kind)` — method and class with the same name are kept separate |

If the same method is called multiple times in the range, only the first occurrence is shown.

## No results

If the range contains no non-trivial types, the tool returns:

```
No non-trivial types found in the specified range.
```

## Implementation

```
CodeLensTool
└── CodeLensService (orchestrator)
    ├── IWorkspaceProvider.GetSolutionAsync()  — loads / reuses cached MSBuild workspace
    ├── Document.GetSemanticModelAsync()       — Roslyn semantic model for the file
    ├── root.DescendantNodes(span)             — syntax nodes within the line range
    ├── CodeLensCollector                      — classifies nodes, resolves types, deduplicates
    │   ├── CodeLensTypeFormatter              — GetDisplayName(), TrimSystemNamespace()
    │   └── CodeLensTypeChecker                — IsTrivial(), IsTrivialDisplayName()
    └── CodeLensFormatter                      — renders sections into markdown string
```

### CodeLensTypeChecker — Nullable handling

Nullable types are unwrapped at the Roslyn symbol level before the triviality check:

```
INamedTypeSymbol { OriginalDefinition.SpecialType == System_Nullable_T }
  → unwrap TypeArguments[0] → check inner type
```

This correctly handles `int?`, `long?`, `Animal?` etc. — the nullable wrapper itself is never
shown as a separate type; only the inner type is evaluated.

### Deduplication keys

Each category uses a dedicated `record` as the `HashSet<T>` key — not a string representation:

```csharp
record VariableKey(string Name, string TypeFullName);
record CallKey(string MethodName);
record ExtensionKey(string MethodName);
record ConstructorKey(string TypeFullName);
record DefinitionKey(string Name, CodeLensEntryKind Kind);
```

## Typical agent workflow

```
1. read_cs_file_digest(filePath)              → see all members with line positions
2. code_lens(filePath, startLine=27, endLine=52)  → resolve all types in the method body
3. get_symbol_details(fullTypeName)           → drill into a specific type if needed
```
