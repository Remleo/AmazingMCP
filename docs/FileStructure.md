# get_file_structure — File Structure Outline

## Purpose

Returns the structural outline of any `.cs` source file without loading a solution or compiling.
Designed to help agents navigate large files: instead of reading thousands of lines blindly,
the agent calls `get_file_structure` first, then reads only the specific member it needs.

## Input

| Parameter | Description |
|---|---|
| `filePath` | Absolute or relative path to a `.cs` file |

## Output format

Each entry appears in source order with a position marker:

```
[line:6, +180 lines, col:1]   — multi-line element: start line, line count, start column
[line:10, col:5]              — single-line element: start line, start column
```

The `+N lines` value is the exact `limit` to pass to `readFile(path, line, limit)` — no math needed.

## What is included

| Element | Notes |
|---|---|
| `namespace` | File-scoped and block-scoped |
| `class` / `interface` / `struct` / `record` | Full signature: modifiers, type params, base list, constraints |
| `enum` | All values with explicit initializers |
| Fields | All access levels, including `private`, `readonly`, `static` |
| Constants | `const` fields with their values |
| Constructors | Full parameter list, `: base()` / `: this()` initializer |
| Methods | Full signature: modifiers, return type, type params, parameters, constraints |
| Properties | Accessor list (`{ get; set; }`, `{ get; init; }`, expression-body shown as `{ get; }`) |
| Indexers | Parameter list + accessors |
| Events | `event` keyword + type + name |
| Operators | `operator` and conversion operators |
| Destructors | `~ClassName()` |
| Nested types | Recursively, with increased indentation |
| Attributes | Printed on the line before the member they decorate |
| `#region` | Shown as encountered in source order |

Private members **are included** — unlike `get_symbol_info` which filters by accessibility.

## Example output

```
namespace TestProject.App.Services  [line:7, +35 lines, col:1]
    public class AnimalService : IAnimalService  [line:9, +33 lines, col:1]
        readonly IAnimalRepository _repository  [line:11, col:32]
        readonly INotificationService _notification  [line:12, col:35]
        readonly AnimalSettings _settings  [line:13, col:29]
        public AnimalService(
        IAnimalRepository repository,
        INotificationService notification,
        IOptions<AnimalSettings> settings)  [line:15, +8 lines, col:5]
        public Animal? GetById(int id)  [line:25, +1 lines, col:5]
        public IReadOnlyList<Animal> GetByKind(AnimalKind kind)  [line:28, +1 lines, col:5]
        public void Add(Animal animal)  [line:31, +10 lines, col:5]
```

## Typical agent workflow

```
1. get_file_structure(filePath)          → see all members with positions
2. readFile(filePath, line=31, limit=10) → read only Add() method body
```

## Implementation notes

- Uses `CSharpSyntaxTree.ParseText` — no MSBuild, no compilation, instant
- Accepts both absolute and relative paths (`Path.GetFullPath` is applied)
- Returns `"File not found: <path>"` if the file does not exist
