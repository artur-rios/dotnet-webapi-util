# ArturRios.Util.WebApi 2.0.0 — Hardening & Documentation

**Date:** 2026-07-09
**Status:** Approved (pending written-spec review)

## Goal

Do a full quality pass over the `ArturRios.Util.WebApi` library: improve readability, usability, and
performance; make it more robust by covering edge cases and fixing bugs/exploits; add XML documentation
across the public surface; rewrite the README as a feature overview; and build a Hugo documentation site
under `docs/` mirroring the structure of the `dotnet-data` project's docs.

Breaking changes are permitted — the library ships as **2.0.0**.

## Success criteria

- `dotnet build` on `src/`: **0 errors**, **CS1591 (missing XML doc) warnings ≈ 0**.
- All behavioral changes are covered by tests (TDD); `dotnet test` is green.
- Public namespaces match their folders (no vestigial `.Api.`).
- README gives an at-a-glance overview of every feature area with install + quick starts.
- A buildable Hugo site under `docs/` with an overview, an architecture page, and one page per feature area.

## Non-goals (YAGNI)

- Coverage-report generation / CI wiring (menu link may be added later).
- New runtime features beyond hardening existing ones.
- Concrete `IAuthenticationProvider` implementations.
- Any change to `ArturRios.Output`, `ArturRios.Jwt`, or other upstream packages.

## Feature inventory (what the library actually offers)

| Area | Types |
|---|---|
| Configuration / bootstrap | `WebApiStartup`, `WebApiParameters`, `AppSettingsKeys` |
| Security — auth | `JwtMiddleware`, `JwtAuthenticationOptions`, `JwtValidationMode`, `AuthenticatedUserFactory`, `TokenClaimKeys`, `AuthenticationExtensions`, `IAuthenticationProvider`, `CachedAuthenticationProvider(+Options)`, `AuthenticationServiceCollectionExtensions` |
| Security — authorization | `AuthorizeAttribute`, `RoleRequirementAttribute`, `RoleRequirementFilter`, `AllowAnonymousAttribute` |
| Security — records/validation | `AuthenticatedUser`, `Authentication`, `Credentials`, `CredentialsValidator` |
| Middleware | `WebApiMiddleware` (marker), `ExceptionMiddleware`, `TraceActivityMiddleware` |
| HTTP client | `BaseWebApiClient`, `BaseWebApiClientRoute` |
| Handlers | `TracePropagationHandler` |
| ASP.NET Core helpers | `ResponseResolver` |

## Work items

### A. Namespace alignment (breaking)

- `ArturRios.Util.WebApi.Api.Configuration` → `ArturRios.Util.WebApi.Configuration`
  (`WebApiStartup`, `WebApiParameters`, `AppSettingsKeys`).
- `ArturRios.Util.WebApi.Api.Client` → `ArturRios.Util.WebApi.Client`
  (`BaseWebApiClient`, `BaseWebApiClientRoute`).
- Update every `using`/reference (e.g. `JwtMiddleware`, `WebApiStartup`).

### B. Bug fixes, robustness, exploit hardening (each TDD'd)

1. **Client auth header is not idempotent** — `BaseWebApiClientRoute.Authorize` uses
   `DefaultRequestHeaders.Add("Authorization", …)`, which throws/duplicates on a second call. Switch to
   `DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token)`.
2. **JWT bearer parsing** — `JwtMiddleware` extracts the token via `Split(' ').Last()`. Parse the
   `Authorization` header properly: require the `Bearer` scheme (case-insensitive), trim, and treat a
   missing/malformed header as an unauthenticated request (401) rather than passing garbage to the handler.
3. **Per-request global mutation** — `TraceActivityMiddleware` sets `Activity.DefaultIdFormat` and
   `Activity.ForceDefaultIdFormat` on every request. Move to a one-time static initialization.
4. **File/class typo** — rename `TraceActivityMiddlware.cs` → `TraceActivityMiddleware.cs` (class already
   spelled correctly).
5. **Throwaway DI container** — `WebApiStartup.LoadConfiguration` calls `BuildServiceProvider()` and
   disposes it via `using`, building a second container and disposing singletons. Refactor to construct the
   `ConfigurationLoader` without a throwaway provider.
6. **Generic exception** — `BaseWebApiClientRoute.AuthenticateAsync` throws `new Exception(...)`. Introduce
   a dedicated `WebApiClientException`.
7. **ExceptionMiddleware logging** — log via `logger.LogError(exception, "…")` (structured, single call
   with the exception object) instead of separate `.Message`/`.StackTrace` string logs. Confirm the client
   response never leaks internals (keep the generic message unless `CustomException`).
8. **Validation rules** — `CredentialsValidator` gains an email-format rule and a minimal password-length
   rule (in addition to `NotEmpty`).
9. **Small guards** — null/empty guards on `BaseWebApiClient` base URL; defensive checks where cheap.

### C. Readability

- Consistent middleware entrypoint naming: standardize on `InvokeAsync` (ASP.NET convention;
  `TraceActivityMiddleware` already uses it) across `ExceptionMiddleware` and `JwtMiddleware`. Both names
  are resolved by `UseMiddleware` convention, so this is safe; only in-repo test call sites update.
- Extract remaining magic strings (`"Authorization"`, `"Bearer"`, `"User"`, `"traceparent"`) to constants.
- Keep files single-purpose; no file grows a second responsibility.

### D. XML documentation

- `///` summaries (and `<param>`/`<returns>` where useful) on every public type and member across all
  areas, so `GenerateDocumentationFile` produces a complete XML and CS1591 warnings drop to ~0.

### E. README overhaul

- Replace the stub with: one-paragraph intro, install, a feature-area overview table, a short quick start
  per area (bootstrap, security, middleware, client, responses), and links to the docs site + license.
  Depth comparable to the `dotnet-data` README.

### F. Hugo documentation site (mirrors dotnet-data)

- `hugo.toml`: set `baseURL = 'https://artur-rios.github.io/dotnet-webapi-util'`, `title`, `[params]`
  (author, logo), and `[menu]` entries per page + Author/GitHub links. Same `hugo-theme-re-terminal`.
- Content pages (TOML `+++` front-matter, mermaid diagrams like dotnet-data):
  - `content/_index.md` — overview, feature map (mermaid), install, envelope note, "where to next".
  - `content/architecture.md` — request pipeline, middleware order, the security/auth model, and how the
    pieces relate (mermaid flow + class diagrams). Design principles.
  - `content/configuration.md` — `WebApiStartup` lifecycle, `WebApiParameters` (CLI args), Swagger, logging,
    invalid-model-state response.
  - `content/security.md` — JWT middleware + `JwtValidationMode` (ClaimsOnly vs Revalidate), the caching
    provider, `IAuthenticationProvider`, authorize/role attributes, credentials + validation.
  - `content/middleware-and-diagnostics.md` — `ExceptionMiddleware`, `TraceActivityMiddleware`,
    `TracePropagationHandler`, the `WebApiMiddleware` marker + registration via `AddMiddlewares`.
  - `content/http-client.md` — `BaseWebApiClient` / `BaseWebApiClientRoute`, authenticate/authorize flow,
    trace propagation.
  - `content/responses.md` — `ResponseResolver` and the `DataOutput`/`ProcessOutput`/`PaginatedOutput`
    envelopes, default status-code mapping.

### G. Testing & verification

- TDD each behavioral change in `tests/` (xUnit). Keep the existing 15 tests green.
- Final gate: `dotnet build` (0 errors, ~0 CS1591) + `dotnet test` (all green) + `hugo` build of `docs/`
  succeeds (or a content-lint check if Hugo isn't installed).

### H. Versioning

- Bump `<Version>` in `src/ArturRios.Util.WebApi.csproj` to `2.0.0`.

## Risks / notes

- Namespace changes ripple to every consumer; this is the headline breaking change and the reason for the
  major bump. It will be called out in the README/docs.
- If the `hugo` CLI is unavailable in this environment, the site will be validated structurally (front
  matter, links, menu) rather than by a full render.
