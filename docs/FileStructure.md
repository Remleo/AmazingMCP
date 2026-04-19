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

Each entry is the original declaration from source (with body stripped), followed by a position marker:

```
[lines:95 +54]   — multi-line element: start line, line count
[line:28]         — single-line element: start line only
```

The `+N` value is the exact `limit` to pass to `readFile(path, line, limit)` — no math needed.

## What is included

| Element | Notes |
|---|---|
| `usings` | Collapsed into one line: range from first to last `using`, comments between are included in the range |
| `namespace` | File-scoped and block-scoped |
| `class` / `interface` / `struct` / `record` | Original signature from source, body stripped |
| `enum` | All values with explicit initializers |
| Fields | All access levels including `private`, `readonly`, `static`, with initializers |
| Constants | `const` fields with their values |
| Constructors | Full parameter list, `: base()` / `: this()` initializer |
| Methods | Full signature, body stripped, terminated with `;` |
| Properties | Auto-properties kept as-is; expression-body shown as `{ get; }`; block-body accessors reduced to `{ get; set; }` |
| Indexers | Parameter list + accessors |
| Events | `event` keyword + type + name |
| Operators | `operator` and conversion operators |
| Destructors | `~ClassName()` |
| Nested types | Recursively, with increased indentation |
| Attributes | Printed on the line before the member they decorate |
| XML doc `<summary>` | Printed as `/// text` before the member, max 200 chars, truncated with `…` |

Private members **are included** — unlike `get_symbol_info` which filters by accessibility.

## Example output

```
usings  [lines:4 +18]
namespace Bwin.Sports.Aggregation.KafkaClient.Consumer  [lines:24 +1061]
    public class MessageConsumer : IMessageConsumer  [lines:26 +1058]
        private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(5);  [line:31]
        /// Kafka default for max.poll.interval.ms (300 000 ms). Used as a fallback...
        private static readonly TimeSpan DefaultMaxPollInterval = TimeSpan.FromMilliseconds(300_000);  [line:38]
        private readonly ILogger logger;  [line:60]
        public event EventHandler<ConsumerGroupInfo>? OnConsumerGroupInfo;  [line:93]
        public MessageConsumer( KafkaConsumerConfig configuration, ...);  [lines:95 +54]
        public async Task StartAsync(CancellationToken cancellationToken);  [lines:151 +50]
        private void UpdateStatistics(string json);  [lines:776 +89]
```

## Typical agent workflow

```
1. get_file_structure(filePath)          → see all members with positions
2. readFile(filePath, line=776, limit=89) → read only UpdateStatistics() method body
```

## Implementation notes

- Uses `CSharpSyntaxTree.ParseText` — no MSBuild, no compilation, instant
- Signatures are taken directly from source text with bodies stripped, preserving original formatting
- XML doc `<summary>` is extracted from leading trivia, normalized to single line, capped at 200 chars
- Accepts both absolute and relative paths (`Path.GetFullPath` is applied)
- Returns `"File not found: <path>"` if the file does not exist
