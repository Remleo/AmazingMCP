# code_lens — Type Resolution for a Code Span

## Purpose

`code_lens` resolves all type names in a given line range of a `.cs` file to their fully qualified forms —
with namespace, generic arguments, and nullability.

Use it whenever you need to know the exact full type of something appearing in a code span:
a local variable, a field/property access, a method call, or a declaration.

## Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `filePath` | Yes | Absolute or relative path to the `.cs` file to analyze |
| `startLine` | Yes | First line of the range to analyze (1-based, inclusive) |
| `endLine` | Yes | Last line of the range to analyze (1-based, inclusive) |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

## What is collected

The tool walks all syntax nodes within the requested span and classifies them into sections:

| Section | What triggers it | What is shown |
|---|---|---|
| **Variables** | Read/write usages of local variables (not declarations) | Full type name per unique variable (deduplicated by name + type) |
| **Fields** | Field accesses on the **current class only** (`this.Field` or bare `Field`) | Full type name of the field |
| **Properties** | Property accesses on the **current class only** | Full type name of the property |
| **Calls** | Instance and static method calls (non-System declaring type) | Pseudo-signature from the method definition |
| **Extensions** | Extension method calls (non-System declaring type) | Pseudo-signature with `this` receiver |
| **Object Creations** | `new T(...)` and `new(...)` with arguments | Constructor pseudo-signature |
| **Definitions** | Method, constructor, field, property, class, interface, record, struct declarations | Full-type signatures and member types |

### Variables

Only **usages** of local variables are collected — not their declarations.
A declaration like `var animal = _repository.FindById(id)` does not produce a Variables entry
because the type is already visible from the **Calls** section (return type of `FindById`).
The entry appears when the variable is actually read or passed somewhere:

```csharp
return animal;          // → var animal: TestProject.Core.Models.Animal
_repository.Save(animal); // → same entry, deduplicated
```

This is most useful when the variable is declared outside the requested span
and only its usages fall within the analyzed range.

### Fields and Properties filtering

Only members of the **nearest enclosing class** in the span are collected:
- `this.SomeField` — collected ✓
- bare `SomeField` (implicit this) — collected ✓
- `anyVar.SomeProperty` — **skipped** ✗
- `SomeMethod().SomeProperty` — **skipped** ✗

### System type filtering

Calls and extension methods whose **declaring type** namespace starts with `System` are skipped entirely.
This removes noise from LINQ, BCL helpers, etc.

### Trivial type filtering

The following types are considered trivial and omitted from Variables, Fields, Properties, return types, and Definition params:

- Primitives: `string`, `bool`, `int`, `uint`, `long`, `ulong`, `short`, `ushort`, `byte`, `sbyte`, `float`, `double`, `decimal`, `char`, `object`, `void`
- `System.Threading.CancellationToken`
- `System.Threading.Tasks.Task` and `System.Threading.Tasks.ValueTask` (without generic argument)
- `Nullable<T>` where `T` is itself trivial (e.g. `int?`, `bool?`)
- `Task<T>` and `ValueTask<T>` where `T` is trivial (e.g. `Task<int>`)

## Output format

The output starts with the requested source snippet for reference, followed by sections.
Each section is only shown if it has entries.

````
```csharp
var animal = _repository.FindById(id);
return animal;
```

## Variables
var animal: TestProject.Core.Models.Animal

## Fields
_repository: TestProject.Core.Persistence.IAnimalRepository

## Calls
TestProject.Core.Models.Animal FindById(int id) from TestProject.Core.Persistence.IAnimalRepository

## Extensions
IEnumerable<TestProject.Core.Models.Animal> Where(this IReadOnlyList<TestProject.Core.Models.Animal> _, Func<TestProject.Core.Models.Animal, Boolean> predicate) from TestProject.Core.Extensions.AnimalExtensions

## Object Creations
new TestProject.App.Services.OrderService(TestProject.Core.Persistence.IAnimalRepository repository, TestProject.Core.Services.INotificationService notification)

## Definitions
TestProject.Core.Models.Animal FindById(int id)
ctor(TestProject.Core.Persistence.IAnimalRepository repository, TestProject.Core.Services.INotificationService notification)
field TestProject.Core.Persistence.IAnimalRepository _repository
prop TestProject.Core.Models.Animal CurrentAnimal
TestProject.App.Services.AnimalService(TestProject.Core.Persistence.IAnimalRepository repo, TestProject.Core.Services.INotificationService svc) : BackgroundService, TestProject.Core.Services.IAnimalService
````

### Call pseudo-signature format

```
{ReturnType} MethodName({ArgType} argName, ...) from {DeclaringType}
```

- `ReturnType` is omitted when trivial (void, int, bool, etc.)
- `from {DeclaringType}` is omitted when the method belongs to the **current class**
- All types are fully qualified per System-trimming rules

### Extension pseudo-signature format

```
{ReturnType} MethodName(this {ReceiverType} _, {ArgType} argName, ...) from {DeclaringType}
```

The first parameter always has `this` prefix to indicate it is an extension method.

### Constructor pseudo-signature format

```
new {TypeFullName}({ArgType} argName, ...)
```

Constructors with zero arguments are omitted.

### Definition format

| Kind | Format |
|---|---|
| Method | `{ReturnType} MethodName({ArgType} argName, ...)` — `ReturnType` always shown, including `void` |
| Constructor | `ctor({ArgType} argName, ...)` |
| Field | `field {TypeFullName} fieldName` |
| Property | `prop {TypeFullName} PropertyName` |
| Type | `{TypeFullName}({primaryCtorParams}) : BaseType, IInterface` |

### System namespace trimming

Types whose namespace starts with `System.` have the namespace stripped — only the short name
and generic arguments are shown:

| Full name | Displayed as |
|---|---|
| `System.Collections.Generic.IEnumerable<MyApp.Core.Item>` | `IEnumerable<MyApp.Core.Item>` |
| `System.Collections.Generic.List<MyApp.Core.Item>` | `List<MyApp.Core.Item>` |
| `System.Threading.Tasks.Task<MyApp.Core.Result>` | `Task<MyApp.Core.Result>` |
| `System.Func<MyApp.Core.Item, System.Boolean>` | `Func<MyApp.Core.Item, Boolean>` |
| `System.Threading.CancellationToken` | `CancellationToken` |

Non-`System.*` namespaces (project types, NuGet types) are always shown in full.

### Deduplication

Each section deduplicates by a typed record key:

| Section | Deduplication key |
|---|---|
| Variables | `(Name, TypeFullName)` — only usages; declarations are ignored |
| Fields | `(Name, TypeFullName)` |
| Properties | `(Name, TypeFullName)` |
| Calls | `(MethodName, ParamTypes, DeclaringType)` — overloads produce separate entries |
| Extensions | `(MethodName, ParamTypes, DeclaringType)` |
| Object Creations | `(TypeFullName, ParamTypes)` |
| Definitions | `(Name, Kind)` — method and class with the same name are kept separate |

## No results

If the range contains no non-trivial types, the tool returns the source snippet followed by:

```
No non-trivial types found in the specified range.
```

## Implementation

```
CodeLensTool
└── CodeLensService (orchestrator)
    ├── IWorkspaceProvider.GetSolutionAsync()  — loads / reuses cached MSBuild workspace
    ├── Document.GetSemanticModelAsync()       — Roslyn semantic model for the file
    ├── root.FindNode(span).Ancestors()        — resolves nearest enclosing TypeDeclarationSyntax → containingType
    ├── root.DescendantNodes(span)             — syntax nodes within the line range
    ├── CodeLensCollector (dispatcher)
    │   ├── VariableCollector                  — local variable usages only (IdentifierNameSyntax → ILocalSymbol; declarations skipped)
    │   ├── InvocationCollector                — method calls and extension calls (signature from definition)
    │   ├── ObjectCreationCollector            — new T(...) and new(...) (constructor signature)
    │   ├── MemberAccessCollector              — field and property reads (current class only)
    │   └── DefinitionCollector                — type, method, field, property declarations
    │       ├── CodeLensTypeFormatter          — GetDisplayName(), TrimSystemNamespace()
    │       └── CodeLensTypeChecker            — IsTrivial(), IsTrivialDisplayName()
    └── CodeLensFormatter                      — renders sections into markdown string
```

## Typical agent workflow

```
1. read_cs_file_digest(filePath)                      → see all members with line positions
2. code_lens(filePath, startLine=27, endLine=52)      → resolve all types in the method body
3. get_symbol_details(fullTypeName)                   → drill into a specific type if needed
```
