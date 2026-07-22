# Decouple ArturRios.Util.WebApi from ArturRios.Logging

**Date:** 2026-07-22
**Status:** Approved (pending written-spec review)

## Goal

Remove the library's dependency on `ArturRios.Logging`. Logging setup becomes entirely the
responsibility of the derived `WebApiStartup` class. The classes that emit log entries stay
log-tool agnostic by depending only on `Microsoft.Extensions.Logging.ILogger<T>`, which any
backend (the .NET native provider, Serilog, or a custom `ILogger`) satisfies.

This is item **A** of a three-part effort. Items B (configurable header/cookie token source)
and C (Google authentication) are designed separately.

## Background

- Only [`WebApiStartup`](../../../src/Configuration/WebApiStartup.cs) references `ArturRios.Logging`
  (four `using` lines plus the two helper methods below).
- `AddLogging()` — registers the default ASP.NET Core logging services.
- `AddCustomLogging(List<LoggerConfiguration>)` — clears providers, registers `IStateLogger`/
  `StateLogger`, and calls the `AddCustomLogger()` extension, all from `ArturRios.Logging`.
- The classes that actually log — [`ExceptionMiddleware`](../../../src/Middleware/ExceptionMiddleware.cs)
  and [`TraceActivityMiddleware`](../../../src/Middleware/TraceActivityMiddleware.cs) — already depend
  only on `ILogger<T>`. `LoadConfiguration` likewise uses `ILogger<ConfigurationLoader>` and
  `LoggerFactory`. None of these reference `ArturRios.Logging`, so they need no change.
- The library does **not** wire `AddLogging` into any lifecycle method itself; derived classes call
  the helpers explicitly. `WebApplication.CreateBuilder(args)` already registers the default logging
  providers, so the app still logs after the helpers are removed.

## Success criteria

- No file under `src/` references `ArturRios.Logging` (any namespace).
- The `ArturRios.Logging` `PackageReference` is gone from
  [`ArturRios.Util.WebApi.csproj`](../../../src/ArturRios.Util.WebApi.csproj).
- `dotnet build` on `src/`: 0 errors, no new warnings.
- `dotnet test`: green (no test referenced the removed methods).
- Docs no longer reference `AddLogging()` / `AddCustomLogging(...)`.

## Non-goals (YAGNI)

- Introducing a project-owned logging abstraction. `ILogger<T>` is already tool-agnostic and stays.
- Any change to `ExceptionMiddleware`, `TraceActivityMiddleware`, or `ConfigurationLoader` logging.
- Providing a replacement logging helper. Logging setup moves fully to the derived class.

## Changes

### 1. `src/Configuration/WebApiStartup.cs`
- Delete the four `using ArturRios.Logging.*;` lines.
- Delete `AddLogging()`.
- Delete `AddCustomLogging(List<LoggerConfiguration>)` (removes the `IStateLogger`/`StateLogger`/
  `AddCustomLogger()` registrations).
- Keep `using Microsoft.Extensions.Logging;` — still used by `LoadConfiguration`.
- Update the class-level XML `<summary>` to drop the "logging" claim (currently "wires up
  configuration, logging, middlewares and Swagger").

### 2. `src/ArturRios.Util.WebApi.csproj`
- Remove `<PackageReference Include="ArturRios.Logging" Version="1.0.0" />`.

### 3. Documentation
- `README.md` (~line 46): remove the `AddLogging();` call from the startup example.
- `docs/content/configuration.md`: remove the `AddLogging() / AddCustomLogging(...)` mermaid node,
  the two method-table rows, and the explanatory paragraph. Replace with a one-line note that
  logging setup is the derived class's responsibility and any `ILogger`-compatible backend works.

## Breaking change / versioning

Removing two public methods and a package dependency is a breaking API change. The library is
currently at `1.1.0`. Recommend bumping to a new **major** version as part of this change. Final
version number to be confirmed at implementation time.

## Verification

1. `dotnet build src` — 0 errors, no new warnings.
2. `dotnet test` — green.
3. `grep -r "ArturRios.Logging" src/` — no matches.
