# read_cs_file_digest / read_large_cs_file — Token-Efficient File Reading

## Purpose

Two complementary tools for reading large `.cs` files without loading the entire content.
The typical workflow is: call `read_cs_file_digest` first to get a structural overview, then call `read_large_cs_file` to load only the specific members you need.

This saves significant tokens when working with files that have hundreds or thousands of lines (e.g. large services, test fixtures, generated code).

---

## read_cs_file_digest

Returns a compact digest of a `.cs` file: all namespaces, types, and members with line numbers — without implementations. Uses `CSharpSyntaxTree.ParseText` — no MSBuild, no compilation, instant.

### Input

| Parameter | Description |
|---|---|
| `filePath` | Absolute path to the `.cs` file |

### Output format

Each entry is the original declaration from source (with body stripped), followed by a position marker in a comment:

```
/*[lines:95 +54]*/   — multi-line element: start line, line count
/*[line:28]*/         — single-line element: start line only
```

The `+N` value is the exact `limit` to pass to `read_large_cs_file` to read that member.

A hint is appended at the end of every digest output:
```
> PREFER `read_large_cs_file` over reading the raw file — shows real source of any member by name/signature without loading the whole file.
> Examples: ["*ProcessAsync*"], ["usings", "*public*"], ["*Async*"]
```

### What is included

| Element | Notes |
|---|---|
| `usings` | Collapsed into one entry: range from first to last `using` |
| `namespace` | File-scoped and block-scoped |
| `class` / `interface` / `struct` / `record` | Original signature, body stripped |
| `enum` | All values with explicit initializers |
| Fields | All access levels, with initializers |
| Constants | `const` fields with their values |
| Constructors | Full parameter list, `: base()` / `: this()` initializer |
| Methods | Full signature, body stripped, terminated with `;` |
| Properties | Auto-properties as-is; expression-body shown as `{ get; }`; block-body reduced to `{ get; set; }` |
| Indexers | Parameter list + accessors |
| Events | `event` keyword + type + name |
| Operators | `operator` and conversion operators |
| Destructors | `~ClassName()` |
| Attributes | Printed on the line before the member they decorate |
| XML doc `<summary>` | Printed as `/// text` before the member, truncated with `…` |
| Nested types | Recursively, with increased indentation |

### Example output

```
/*[lines:1 +18]*/ usings
/*[line:20]*/ namespace AmazingMCP.Services.Workspace
/*[lines:22 +198]*/ public class SolutionLoader : ISolutionLoader
    /*[line:24]*/ readonly ILogger<SolutionLoader> _logger;
    /*[lines:26 +1]*/ public SolutionLoader(ILogger<SolutionLoader> logger);
    /*[lines:29 +40]*/ public async Task<Solution> LoadAsync(string solutionPath, CancellationToken ct);
    /*[lines:71 +15]*/ void LogWarnings(ImmutableArray<Diagnostic> diagnostics);
```

### Implementation

```
ReadCsFileDigestService
├── IFileReader.ReadAllText(filePath)         — reads raw source
└── ISourceDigestService.GetDigest(source)    — parses and formats
    └── SourceDigestService
        ├── CSharpSyntaxTree.ParseText()      — parse-only, no compilation
        ├── AppendUsings()                    — collapses all usings into one range entry
        ├── WalkNodes()                       — recursive walk: namespace → type → member
        │   ├── SyntaxNodeFormatter.Sig()     — formats namespace/type signature
        │   ├── MemberSignatureExtractor      — strips bodies from members
        │   │   ├── Auto-properties kept as-is
        │   │   ├── Expression-body props → `{ get; }`
        │   │   ├── Block-body props → `{ get; set; }` (accessor modifiers preserved)
        │   │   └── Methods/ctors: body stripped, terminated with `;`
        │   └── XmlDocExtractor               — extracts <summary>, truncated to 200 chars
        └── SyntaxNodeFormatter.PosWithLeadingTrivia() — line position including attributes
```

---

## read_large_cs_file

Returns only the members matching the given wildcard filters — with full implementations. Respects `ReadCsOptions.ReadOutputMaxLength` (default 20 000 chars).

### Input

| Parameter | Description |
|---|---|
| `filePath` | Absolute path to the `.cs` file |
| `filters` | Wildcard filter patterns matched against member names. Pass `[]` to return the full file. Use `.ctor` for constructors, `usings` for using directives. |

### Filter matching

Filters are matched against the member's **name** (not signature). Special aliases:
- `.ctor` — matches all constructors
- `usings` — matches the using directives block

Wildcards use glob syntax: `*` matches any sequence of characters.

### Filter examples

| Filter | Matches |
|---|---|
| `["*Async*"]` | All members whose name contains `Async` |
| `[".ctor"]` | All constructors |
| `["usings"]` | Using directives block |
| `["Load*", ".ctor"]` | Members starting with `Load` plus constructors |
| `[]` | Full file (no filtering) |

### Output behavior

- **No filters, file ≤ max length** — returns full source
- **No filters, file > max length** — returns error message suggesting to use filters or `read_cs_file_digest`
- **With filters, result ≤ max length** — returns matched members with surrounding context
- **With filters, result > max length** — truncates and appends a hint to use narrower filters
- **No matches** — returns `// No matches found.` with a hint to check member names via `read_cs_file_digest`

Gaps between matched sections are replaced with `// << ... cut ... >>`.

### Implementation

```
ReadLargeCsFileService
├── IFileReader.ReadAllText(filePath)
└── IFilteredSourceService.GetFilteredSource(source, filters)
    └── FilteredSourceService
        ├── IFileStructureService.GetItems(source)   — builds FileStructureItem list via Roslyn parse
        ├── IWildcardPatternFactory.CreateGlob()     — compiles filter patterns
        ├── CollectMatchedRanges()                   — finds items matching any filter
        │   └── GetItemEndLine()                     — for large types (>50 lines): only declaration line
        │                                              for small types: full body
        ├── AddContainerTypeDeclarations()           — adds enclosing type declaration lines
        ├── AddNamespaceDeclarations()               — always includes namespace declaration lines
        ├── MergeRanges()                            — merges overlapping/adjacent ranges
        └── BuildOutput()                            — assembles output with // << ... cut ... >> gaps
```

---

## Typical agent workflow

```
1. read_cs_file_digest(filePath)               → see all members with line positions
2. read_large_cs_file(filePath, ["LoadAsync"]) → read only the LoadAsync implementation
```
