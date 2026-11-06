# Security Policy

## Supported versions

Only the latest version published on [NuGet](https://www.nuget.org/packages/HoldMyCoolantMeatbag.AmazingMCP) receives security fixes.

## Reporting a vulnerability

Please do not open a public issue for security problems.
Report them privately through [GitHub Security Advisories](https://github.com/Remleo/AmazingMCP/security/advisories/new).

You can expect a first response within a week.

## Scope

AmazingMCP is a read-only local server: it loads and compiles solutions in memory and never modifies source files.
It has no authentication and listens on `localhost` by default. Binding it to other interfaces exposes your source code to anyone who can reach the port.
