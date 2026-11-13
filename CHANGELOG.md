# Changelog

## [2.0.0] — 2026-11-13

- `query_symbol`: **breaking** — `query` is replaced by `typeQuery`, `typeExclude` and `memberQuery`; members can now be searched within namespaces or types

## [1.5.0] — 2026-06-19

- Added `--HttpServerTransport:IdleTimeout` option to configure MCP session idle timeout (default: 7 days)

## [1.4.0] — 2026-06-18

- `query_usages`: added `EventSubscribe`, `EventUnsubscribe`, `EventCall` usage kinds
- `query_usages`: fixed null-conditional extension method detection
- `query_usages`: usages via concrete types are now attributed to the interface member
