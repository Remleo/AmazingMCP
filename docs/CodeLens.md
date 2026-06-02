# code_lens — Type Resolution for a Code Span

## Purpose

`code_lens` resolves all type names in a given line range of a `.cs` file to their fully qualified forms — with namespace, generic arguments, and nullability. Uses a live Roslyn semantic model.

Use it whenever you need to know the exact full type of something appearing in a code span: a local variable, a field/property access, a method call, or a declaration.

## Input

| Parameter | Required | Description |
|---|---|---|
| `solutionWorkspacePath` | Yes | Absolute path to the directory containing the `.sln`/`.slnx` file |
| `filePath` | Yes | Absolute or relative path to the `.cs` file to analyze |
| `startLine` | Yes | First line of the range to analyze (1-based, inclusive) |
| `endLine` | Yes | Last line of the range to analyze (1-based, inclusive) |
| `solutionPath` | No | Explicit path to the `.sln`/`.slnx` file (required only when multiple solutions exist) |

## Output format

The output starts with the requested source snippet in a `csharp` block, followed by all resolved entries in a plain code block, sorted by source line:

````
```csharp
var animal = _repository.FindById(id);
return animal;
```

```
scope `MyApp.Services.AnimalService`
field `MyApp.Core.Persistence.IAnimalRepository _repository`
call `Animal FindById(int id)` from `MyApp.Core.Persistence.IAnimalRepository`
var `MyApp.Core.Models.Animal animal`
```
````

If no non-trivial types are found: `No non-trivial types found in the specified range.`

### Entry format per kind

| Kind | Format |
|---|---|
| Variable (local) | `var \`Type name\`` |
| Field | `field \`Type name\`` |
| Property | `prop \`Type name\`` |
| Method call | `call \`ReturnType MethodName(Type name, ...)\` from \`DeclaringType\`` |
| Extension call | `call ext \`ReturnType MethodName(this ReceiverType _, Type name, ...)\` from \`DeclaringType\`` |
| Constructor | `new \`ShortTypeName(Type name, ...)\`` |
| Method definition | `def \`ReturnType MethodName(Type name, ...)\`` |
| Constructor definition | `ctor \`ClassName(Type name, ...)\`` |
| Field/property definition | `field \`Type name\`` / `prop \`Type name\`` |
| Type definition | `def \`ShortTypeName(primaryCtorParams) : BaseType, IInterface\`` |
| Enclosing type | `scope \`FullTypeName\`` |

Notes:
- `from \`DeclaringType\`` is omitted when the method belongs to the current class
- Return type is omitted from `call` entries when trivial (void, int, bool, etc.)
- All entries are sorted by source line

## What is collected

### Variables
Only **usages** of local variables — not their declarations. A `var x = ...` declaration does not produce a Variables entry; the type is already visible from the Calls section (return type). Entries appear when the variable is read or passed somewhere.

Most useful when the variable is declared outside the requested span and only its usages fall within the analyzed range.

### Fields and Properties
Only members of the **nearest enclosing class** in the span:
- `this.SomeField` — collected ✓
- bare `SomeField` (implicit this) — collected ✓
- `anyVar.SomeProperty` — **skipped** ✗
- `SomeMethod().SomeProperty` — **skipped** ✗

### Calls
Instance and static method calls. Extension methods are shown as `call ext`. System.* declaring types are skipped.

### Object Creations
`new T(...)` and `new(...)` (target-typed). Zero-argument constructors are omitted.

### Definitions
Method, constructor, field, property, class, interface, record, struct declarations — only when the declaration identifier starts within the requested span. This prevents a class declared above the range from being included just because its body overlaps.

### Scope
The nearest enclosing type(s) for the span are always shown as `scope` entries.

## Trivial type filtering

The following types are omitted from Variables, Fields, Properties, return types, and Definition params:
- Primitives: `string`, `bool`, `int`, `uint`, `long`, `ulong`, `short`, `ushort`, `byte`, `sbyte`, `float`, `double`, `decimal`, `char`, `object`, `void`
- `CancellationToken`, `Task`, `ValueTask` (without generic argument)
- `Nullable<T>` / `Task<T>` / `ValueTask<T>` where T is itself trivial

## System namespace trimming

Types whose namespace starts with `System.` have the namespace stripped in output:

| Full name | Displayed as |
|---|---|
| `System.Collections.Generic.IEnumerable<MyApp.Core.Item>` | `IEnumerable<MyApp.Core.Item>` |
| `System.Threading.Tasks.Task<MyApp.Core.Result>` | `Task<MyApp.Core.Result>` |
| `System.Func<MyApp.Core.Item, Boolean>` | `Func<MyApp.Core.Item, Boolean>` |

Non-`System.*` namespaces (project types, NuGet types) are always shown in full.

## Deduplication

Each section deduplicates by a typed record key:

| Section | Deduplication key |
|---|---|
| Variables | `(Name, TypeFullName)` — only usages; declarations are ignored |
| Fields | `(Name, TypeFullName)` |
| Properties | `(Name, TypeFullName)` |
| Calls | `(MethodName, ParamTypes, DeclaringType)` |
| Extensions | `(MethodName, ParamTypes, DeclaringType)` |
| Object Creations | `(TypeFullName, ParamTypes)` |
| Definitions | `(Name, Kind)` |

## Implementation

```
CodeLensService (orchestrator)
├── IWorkspaceProvider.GetSolutionAsync()     — loads/reuses cached MSBuild workspace
├── Document.GetSemanticModelAsync()          — Roslyn semantic model for the file
├── root.FindNode(span).Ancestors()           — resolves nearest enclosing TypeDeclarationSyntax → containingType
├── root.DescendantNodes(span)                — syntax nodes within the line range
├── CodeLensCollector (dispatcher per node)
│   ├── IdentifierNameSyntax
│   │   ├── VariableCollector.CollectIdentifierUsage()   — local variable usages only (ILocalSymbol; declarations skipped)
│   │   └── MemberAccessCollector.CollectIdentifier()    — implicit this field/property access
│   ├── InvocationExpressionSyntax
│   │   └── InvocationCollector.Collect()               — method calls, extension calls, static calls
│   ├── ObjectCreationExpressionSyntax / ImplicitObjectCreationExpressionSyntax
│   │   └── ObjectCreationCollector.Collect/CollectImplicit()
│   ├── MemberAccessExpressionSyntax
│   │   └── MemberAccessCollector.CollectMemberAccess()  — explicit field/property access
│   └── Declaration nodes (Method/Constructor/Field/Property/Type)
│       └── DefinitionCollector.*                        — only when identifier starts within span
├── DefinitionCollector.CollectContainingType()          — enclosing type(s) as scope entries
└── CodeLensFormatter.Format()                           — sorts all entries by SourceLine, renders markdown
```

## Typical agent workflow

```
1. read_cs_file_digest(filePath)                      → see all members with line positions
2. code_lens(filePath, startLine=27, endLine=52)      → resolve all types in the method body
3. get_type_details(fullTypeName)                   → drill into a specific type if needed
```
