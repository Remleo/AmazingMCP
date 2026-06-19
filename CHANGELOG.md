# Changelog

## [1.5.0] — 2026-06-19

- Added `--HttpServerTransport:IdleTimeout` option to configure MCP session idle timeout (default: 7 days)

## [1.4.0] — 2026-06-18

- `query_usages`: added `EventSubscribe`, `EventUnsubscribe`, `EventCall` usage kinds
- `query_usages`: fixed null-conditional extension method detection
- `query_usages`: usages via concrete types are now attributed to the interface member
