# Decouple Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the `ArturRios.Logging` dependency from `ArturRios.Util.WebApi`, moving all logging setup to the derived `WebApiStartup` class.

**Architecture:** Delete the two `ArturRios.Logging`-based helper methods (`AddLogging`, `AddCustomLogging`) and their `using`s from `WebApiStartup`, drop the NuGet package reference, and update docs. The classes that emit logs (`ExceptionMiddleware`, `TraceActivityMiddleware`, `ConfigurationLoader` usage) already depend only on `Microsoft.Extensions.Logging.ILogger<T>` and are left untouched — they stay tool-agnostic by construction.

**Tech Stack:** .NET 10, xUnit (existing test project), `Microsoft.Extensions.Logging`.

**Note on method:** This is a pure removal/refactor with no new runtime behavior, so there are no new failing unit tests to write. The guardrails are: (1) `dotnet build` succeeds, (2) the existing `dotnet test` suite stays green, (3) `grep` confirms no remaining `ArturRios.Logging` references. Each task ends with those checks.

## Global Constraints

- Target framework: `net10.0` (do not change).
- The classes `ExceptionMiddleware`, `TraceActivityMiddleware`, and the `LoadConfiguration` method MUST continue to depend only on `Microsoft.Extensions.Logging` — do not remove `using Microsoft.Extensions.Logging;` from `WebApiStartup.cs`.
- No new dependencies. This change only removes one.
- Spec: `docs/superpowers/specs/2026-07-22-decouple-logging-design.md`.
- Run all `dotnet` commands from the repo root `D:\Repositories\dotnet-webapi-util`.

---

### Task 1: Remove logging helpers and `ArturRios.Logging` usings from `WebApiStartup`

**Files:**
- Modify: `src/Configuration/WebApiStartup.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: a `WebApiStartup` with no `AddLogging()` / `AddCustomLogging(...)` members and no `ArturRios.Logging` usings. Later tasks (docs) rely on those members being gone.

- [ ] **Step 1: Delete the four `ArturRios.Logging` `using` lines**

Remove these lines (currently lines 5–8) from the top of `src/Configuration/WebApiStartup.cs`:

```csharp
using ArturRios.Logging;
using ArturRios.Logging.Adapter;
using ArturRios.Logging.Configuration;
using ArturRios.Logging.Interfaces;
```

Leave every other `using` in place — in particular keep `using Microsoft.Extensions.Logging;`.

- [ ] **Step 2: Delete the `AddLogging()` method**

Remove this member (including its XML doc comment):

```csharp
/// <summary>Adds the default ASP.NET Core logging services.</summary>
public void AddLogging() => Builder.Services.AddLogging();
```

- [ ] **Step 3: Delete the `AddCustomLogging(...)` method**

Remove this member (including its XML doc comment):

```csharp
/// <summary>Replaces the default logging providers with the custom logger built from <paramref name="loggerConfigurations"/>.</summary>
/// <param name="loggerConfigurations">The logger configurations to apply.</param>
public void AddCustomLogging(List<LoggerConfiguration> loggerConfigurations)
{
    Builder.Services.AddScoped<IStateLogger>(_ => new StateLogger(loggerConfigurations));

    Builder.Services.AddLogging(lb =>
    {
        lb.ClearProviders();
        lb.AddCustomLogger();
        lb.SetMinimumLevel(LogLevel.Trace);
    });
}
```

- [ ] **Step 4: Update the class-level XML `<summary>`**

The class doc currently reads (around lines 22–27):

```csharp
/// Base class for bootstrapping an ASP.NET Core web API: builds the <see cref="WebApplicationBuilder"/> and
/// <see cref="WebApplication"/>, wires up configuration, logging, middlewares and Swagger, and exposes hooks
```

Change "wires up configuration, logging, middlewares and Swagger" to "wires up configuration, middlewares and Swagger" (drop "logging," — the class no longer provides logging helpers).

- [ ] **Step 5: Build to verify the code still compiles**

Run: `dotnet build src`
Expected: `Build succeeded`, 0 errors. (`Microsoft.Extensions.Logging` remains referenced transitively and via `LoadConfiguration`, so no compile error there.)

- [ ] **Step 6: Run the test suite**

Run: `dotnet test`
Expected: all tests pass. No existing test references `AddLogging`/`AddCustomLogging`, so none should break.

- [ ] **Step 7: Confirm no `ArturRios.Logging` references remain in C# source**

Run: `grep -rn "ArturRios.Logging" src --include=*.cs`
Expected: no output (empty).

- [ ] **Step 8: Commit**

```bash
git add src/Configuration/WebApiStartup.cs
git commit -m "refactor: remove logging helpers from WebApiStartup"
```

---

### Task 2: Remove the `ArturRios.Logging` package reference

**Files:**
- Modify: `src/ArturRios.Util.WebApi.csproj`

**Interfaces:**
- Consumes: Task 1's removal of all C# references to `ArturRios.Logging` (required before the package can be dropped without breaking the build).
- Produces: a csproj with no `ArturRios.Logging` package reference.

- [ ] **Step 1: Delete the package reference line**

Remove this line from the `<ItemGroup>` in `src/ArturRios.Util.WebApi.csproj`:

```xml
<PackageReference Include="ArturRios.Logging" Version="1.0.0" />
```

- [ ] **Step 2: Restore and build to verify nothing depended on the package**

Run: `dotnet build src`
Expected: `Build succeeded`, 0 errors. A missing-type error here would mean a leftover reference from Task 1 — go back and remove it.

- [ ] **Step 3: Run the test suite**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 4: Confirm no `ArturRios.Logging` references remain anywhere under `src`**

Run: `grep -rn "ArturRios.Logging" src`
Expected: no output (empty) — including the csproj now.

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Util.WebApi.csproj
git commit -m "chore: drop ArturRios.Logging package reference"
```

---

### Task 3: Update documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/content/configuration.md`

**Interfaces:**
- Consumes: Tasks 1–2 (the methods no longer exist, so docs must stop referencing them).
- Produces: docs with no `AddLogging()` / `AddCustomLogging(...)` references.

- [ ] **Step 1: Remove `AddLogging();` from the README quick-start example**

In `README.md`, inside the `Build()` example (around line 46), delete the line:

```csharp
        AddLogging();
```

so the sequence reads `LoadConfiguration();` directly followed by `AddCustomInvalidModelStateResponse();`.

- [ ] **Step 2: Remove `AddLogging();` from the `configuration.md` minimal-Startup example**

In `docs/content/configuration.md`, inside the "A minimal `Startup`" example (around line 61), delete the line:

```csharp
        AddLogging();
```

- [ ] **Step 3: Remove the logging node from the lifecycle mermaid diagram**

In `docs/content/configuration.md`, delete this line from the `flowchart` (around line 18):

```
    Build --> Logging["AddLogging() / AddCustomLogging(...)"]
```

- [ ] **Step 4: Remove the two logging rows from the members table**

In `docs/content/configuration.md`, delete these two table rows (around lines 44–45):

```
| `AddLogging()` | helper | Adds the default ASP.NET Core logging services. |
| `AddCustomLogging(loggerConfigurations)` | helper | Replaces the default logging providers with a custom logger built from the given configurations. |
```

- [ ] **Step 5: Replace the "Logging" section body**

In `docs/content/configuration.md`, replace the paragraph under the `## Logging` heading (around lines 139–142):

```
`AddLogging()` wires up the default ASP.NET Core logging providers. `AddCustomLogging(loggerConfigurations)`
instead clears the default providers and registers a custom logger (backed by `IStateLogger`) built from
the given `LoggerConfiguration` list, with the minimum level set to `Trace`. Use one or the other, not
both.
```

with:

```
`WebApiStartup` no longer configures logging — that is the derived class's responsibility. Because the
library's logging types depend only on `Microsoft.Extensions.Logging.ILogger<T>`, any backend works:
the default ASP.NET Core providers (already registered by `WebApplication.CreateBuilder`), Serilog
(`Builder.Host.UseSerilog(...)`), or a custom `ILogger`. Configure whichever you prefer inside your
`Build()` override before calling `BuildApp()`.
```

- [ ] **Step 6: Confirm the docs no longer mention the removed helpers**

Run: `grep -rn "AddLogging\|AddCustomLogging" README.md docs/content`
Expected: no output (empty).

- [ ] **Step 7: Commit**

```bash
git add README.md docs/content/configuration.md
git commit -m "docs: drop AddLogging/AddCustomLogging references"
```

---

### Task 4: Bump the package to a major version

**Files:**
- Modify: `src/ArturRios.Util.WebApi.csproj`

**Interfaces:**
- Consumes: Tasks 1–3 (the breaking removals this version records).
- Produces: the new package version.

**Note:** Removing two public methods and a package dependency is a breaking API change, so this is a **major** bump. The plan uses `2.0.0` (from the current `1.1.0`). If items B+C land before this version is published, they belong to the same `2.0.0`; if `2.0.0` is published first, B+C become the next major. Confirm the exact number with the maintainer before publishing.

- [ ] **Step 1: Update the `<Version>` element**

In `src/ArturRios.Util.WebApi.csproj`, change:

```xml
<Version>1.1.0</Version>
```

to:

```xml
<Version>2.0.0</Version>
```

- [ ] **Step 2: Build to confirm the project still packs cleanly**

Run: `dotnet build src`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ArturRios.Util.WebApi.csproj
git commit -m "chore: bump version to 2.0.0"
```

---

## Final verification

- [ ] `dotnet build src` — 0 errors.
- [ ] `dotnet test` — green.
- [ ] `grep -rn "ArturRios.Logging" src` — no output.
- [ ] `grep -rn "AddLogging\|AddCustomLogging" README.md docs/content` — no output.
