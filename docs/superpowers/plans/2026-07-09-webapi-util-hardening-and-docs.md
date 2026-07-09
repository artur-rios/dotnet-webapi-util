# ArturRios.Util.WebApi 2.0.0 — Hardening & Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Do a full quality pass over `ArturRios.Util.WebApi` — readability, robustness, bug/exploit fixes, complete XML docs, a rewritten README, and a Hugo docs site — shipping as 2.0.0.

**Architecture:** A .NET 10 class library (`src/`) with an xUnit test project (`tests/`) and a Hugo docs site (`docs/`, `hugo-theme-re-terminal`). Behavioral changes are TDD'd through the test project; the docs mirror the `dotnet-data` project's Hugo structure.

**Tech Stack:** .NET 10, xUnit, ASP.NET Core (middleware/filters), FluentValidation, Newtonsoft.Json, `ArturRios.Output`/`ArturRios.Jwt`/`ArturRios.Util.Http`/`ArturRios.Configuration`, Hugo.

## Global Constraints

- Target framework: `net10.0`; `LangVersion=latest`; `Nullable=enable`; `ImplicitUsings=enable`.
- Library version becomes `2.0.0` (`src/ArturRios.Util.WebApi.csproj`).
- `GenerateDocumentationFile=true` is on: every public type/member needs XML docs; goal is CS1591 ≈ 0.
- Every behavioral change follows TDD (red → green → commit). Keep the existing 15 tests green.
- Middleware entrypoints standardize on `InvokeAsync`.
- Commands run from repo root `D:\Repositories\dotnet-webapi-util`.
- Test command: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --nologo -v q`
- Build command: `dotnet build src/ArturRios.Util.WebApi.csproj --nologo --no-incremental`

---

### Task 1: Namespace alignment (drop vestigial `.Api.`)

**Files:**
- Modify: `src/Configuration/AppSettingsKeys.cs`, `src/Configuration/WebApiParameters.cs`, `src/Configuration/WebApiStartup.cs`
- Modify: `src/Client/BaseWebApiClient.cs`, `src/Client/BaseWebApiClientRoute.cs`
- Modify (references): `src/Security/Middleware/JwtMiddleware.cs` (`using ArturRios.Util.WebApi.Api.Configuration;`)
- Test: existing suite recompiles (compilation is the test)

**Interfaces:**
- Produces: namespaces `ArturRios.Util.WebApi.Configuration` and `ArturRios.Util.WebApi.Client` (were `...Api.Configuration` / `...Api.Client`).

- [ ] **Step 1: Rename the namespaces.** In the three `Configuration` files change `namespace ArturRios.Util.WebApi.Api.Configuration;` → `namespace ArturRios.Util.WebApi.Configuration;`. In the two `Client` files change `namespace ArturRios.Util.WebApi.Api.Client;` → `namespace ArturRios.Util.WebApi.Client;`.
- [ ] **Step 2: Fix references.** Grep for the old namespaces: `rg "WebApi\.Api\.(Configuration|Client)" src tests`. Update each `using` (notably `JwtMiddleware.cs` uses `ArturRios.Util.WebApi.Api.Configuration`). `AppSettingsKeys` is referenced by `JwtMiddleware` and `WebApiStartup`.
- [ ] **Step 3: Build.** Run the build command. Expected: 0 errors (warnings unchanged).
- [ ] **Step 4: Test.** Run the test command. Expected: 15 passed.
- [ ] **Step 5: Commit.**
```bash
git add -A && git commit -m "refactor!: align Configuration/Client namespaces with folders"
```

---

### Task 2: Idempotent client authorization + dedicated exception

**Files:**
- Create: `src/Client/WebApiClientException.cs`
- Modify: `src/Client/BaseWebApiClientRoute.cs`
- Test: `tests/Client/BaseWebApiClientRouteTests.cs`

**Interfaces:**
- Produces: `WebApiClientException : Exception` (ctors `(string message)`, `(string message, Exception inner)`); `BaseWebApiClientRoute.Authorize(string)` sets the header idempotently.

- [ ] **Step 1: Write the failing test** (`tests/Client/BaseWebApiClientRouteTests.cs`):
```csharp
using System.Net.Http;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Client;

namespace ArturRios.Util.WebApi.Tests.Client;

public class BaseWebApiClientRouteTests
{
    private sealed class TestRoute(HttpGateway gateway) : BaseWebApiClientRoute(gateway)
    {
        public override string BaseUrl => "/test";
        public void CallAuthorize(string token) => Authorize(token);
    }

    [Fact]
    public void Authorize_IsIdempotent_AndSetsBearerScheme()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://example.test") };
        var route = new TestRoute(new HttpGateway(httpClient));

        route.CallAuthorize("token-one");
        route.CallAuthorize("token-two");

        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("token-two", httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }
}
```
- [ ] **Step 2: Run test — expect FAIL** (compile error: `Authorize` protected but exposed via `CallAuthorize`; the current `.Add(...)` throws on the second call → runtime fail). Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --nologo -v q`.
- [ ] **Step 3: Create `WebApiClientException`:**
```csharp
namespace ArturRios.Util.WebApi.Client;

/// <summary>Raised when a <see cref="BaseWebApiClientRoute"/> operation fails, e.g. authentication returns no body.</summary>
public class WebApiClientException : Exception
{
    /// <summary>Initializes a new instance with an error message.</summary>
    public WebApiClientException(string message) : base(message) { }

    /// <summary>Initializes a new instance with an error message and inner exception.</summary>
    public WebApiClientException(string message, Exception innerException) : base(message, innerException) { }
}
```
- [ ] **Step 4: Fix `Authorize` and the throw** in `BaseWebApiClientRoute.cs`. Add `using System.Net.Http.Headers;`. Replace the `Authorize` body with:
```csharp
Gateway.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
```
and change `AuthenticateAsync`'s throw to `throw new WebApiClientException("Could not authenticate: the authentication response contained no body.");`
- [ ] **Step 5: Run test — expect PASS.**
- [ ] **Step 6: Commit.**
```bash
git add -A && git commit -m "fix: make client Authorize idempotent; add WebApiClientException"
```

---

### Task 3: Harden JWT bearer-token parsing

**Files:**
- Modify: `src/Security/Middleware/JwtMiddleware.cs`
- Test: `tests/Security/JwtMiddlewareTests.cs` (add cases)

**Interfaces:**
- Consumes: existing `JwtMiddleware` test helpers (`CreateToken`, `BuildContext`, `CreateMiddleware`).
- Produces: token extraction that requires the `Bearer` scheme (case-insensitive) and rejects malformed headers.

- [ ] **Step 1: Add failing tests** to `JwtMiddlewareTests.cs`:
```csharp
[Fact]
public async Task AcceptsLowercaseBearerScheme()
{
    var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
    var (context, log) = BuildContext(token, provider: null);
    context.Request.Headers.Authorization = $"bearer {token}"; // lowercase scheme
    var middleware = CreateMiddleware(_ => { log.Append("next"); return Task.CompletedTask; }, JwtValidationMode.ClaimsOnly);

    await middleware.InvokeAsync(context);

    Assert.Equal("next", log.ToString());
}

[Fact]
public async Task RejectsNonBearerScheme()
{
    var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
    var (context, log) = BuildContext(token: null, provider: null);
    context.Request.Headers.Authorization = $"Basic {token}";
    var middleware = CreateMiddleware(_ => { log.Append("next"); return Task.CompletedTask; }, JwtValidationMode.ClaimsOnly);

    await middleware.InvokeAsync(context);

    Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    Assert.Empty(log.ToString());
}
```
> Note: this task also renames `Invoke` → `InvokeAsync` (Task 5 covers `ExceptionMiddleware`). Update **all** existing `middleware.Invoke(context)` call sites in `JwtMiddlewareTests.cs` to `InvokeAsync`.
- [ ] **Step 2: Run tests — expect FAIL** (`Basic` currently parsed by `Split(' ').Last()` → passes token through; lowercase already works but `InvokeAsync` doesn't exist yet).
- [ ] **Step 3: Implement.** In `JwtMiddleware.cs` add `using System.Net.Http.Headers;`, rename `public async Task Invoke` → `public async Task InvokeAsync`, and replace the token line with a call to a private helper:
```csharp
var token = ExtractBearerToken(context);
```
```csharp
private static string ExtractBearerToken(HttpContext context)
{
    var header = context.Request.Headers.Authorization.FirstOrDefault();

    if (string.IsNullOrWhiteSpace(header) || !AuthenticationHeaderValue.TryParse(header, out var parsed))
    {
        return string.Empty;
    }

    if (!string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(parsed.Parameter))
    {
        return string.Empty;
    }

    return parsed.Parameter.Trim();
}
```
- [ ] **Step 4: Run tests — expect PASS** (all, including the updated existing ones).
- [ ] **Step 5: Commit.**
```bash
git add -A && git commit -m "fix: require Bearer scheme when parsing JWT auth header; rename to InvokeAsync"
```

---

### Task 4: TraceActivityMiddleware — rename file, one-time Activity init

**Files:**
- Rename: `src/Middleware/TraceActivityMiddlware.cs` → `src/Middleware/TraceActivityMiddleware.cs`
- Modify: the same file (move global config to a static constructor)
- Test: `tests/Middleware/TraceActivityMiddlewareTests.cs`

**Interfaces:**
- Produces: `TraceActivityMiddleware.InvokeAsync(HttpContext)` still sets `context.Items["TraceId"]`, `context.TraceIdentifier`, and the `traceparent` response header.

- [ ] **Step 1: Write the failing test:**
```csharp
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

public class TraceActivityMiddlewareTests
{
    [Fact]
    public async Task SetsTraceIdAndTraceparentHeader()
    {
        var context = new DefaultHttpContext();
        var middleware = new TraceActivityMiddleware(_ => Task.CompletedTask, NullLogger<TraceActivityMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrEmpty(context.Items["TraceId"] as string));
        Assert.Equal(context.Items["TraceId"], context.TraceIdentifier);
        Assert.True(context.Response.Headers.ContainsKey("traceparent"));
    }
}
```
- [ ] **Step 2: Run test — expect FAIL** (namespace/file compiles but assert on behavior; confirm it fails only if wiring is off — if it passes immediately, that's fine as a characterization test for the refactor. If so, note it and proceed; the refactor must keep it green).
- [ ] **Step 3: Rename file & refactor.** `git mv src/Middleware/TraceActivityMiddlware.cs src/Middleware/TraceActivityMiddleware.cs`. Remove the two lines from `InvokeAsync`:
```csharp
Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;
```
and add a static constructor:
```csharp
static TraceActivityMiddleware()
{
    Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    Activity.ForceDefaultIdFormat = true;
}
```
- [ ] **Step 4: Run test — expect PASS.**
- [ ] **Step 5: Commit.**
```bash
git add -A && git commit -m "refactor: fix TraceActivityMiddleware filename typo; init Activity format once"
```

---

### Task 5: ExceptionMiddleware — structured logging + InvokeAsync

**Files:**
- Modify: `src/Middleware/ExceptionMiddleware.cs`
- Test: `tests/Middleware/ExceptionMiddlewareTests.cs`

**Interfaces:**
- Produces: `ExceptionMiddleware.InvokeAsync(HttpContext)`; unhandled exceptions → 500 with a generic JSON message; client-canceled requests do not write a response.

- [ ] **Step 1: Write the failing test:**
```csharp
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task UnhandledException_Writes500_WithGenericMessage()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Internal server error", body);
        Assert.DoesNotContain("boom", body); // no internal leak
    }
}
```
- [ ] **Step 2: Run test — expect FAIL** (`InvokeAsync` does not exist).
- [ ] **Step 3: Implement.** Rename `Invoke` → `InvokeAsync`. Replace the three separate `logger.LogError(...)` string calls in `HandleException` with a single structured call:
```csharp
logger.LogError(exception, "Unhandled exception while processing the request.");
```
(Keep the `CustomException` message extraction and the client response exactly as-is.)
- [ ] **Step 4: Run test — expect PASS.**
- [ ] **Step 5: Commit.**
```bash
git add -A && git commit -m "refactor: structured logging in ExceptionMiddleware; rename to InvokeAsync"
```

---

### Task 6: CredentialsValidator — email format + password length

**Files:**
- Modify: `src/Security/Validation/CredentialsValidator.cs`
- Test: `tests/Security/CredentialsValidatorTests.cs`

**Interfaces:**
- Produces: `CredentialsValidator` rejects invalid email format and passwords shorter than 8 chars.

- [ ] **Step 1: Write the failing test:**
```csharp
using ArturRios.Util.WebApi.Security.Records;
using ArturRios.Util.WebApi.Security.Validation;

namespace ArturRios.Util.WebApi.Tests.Security;

public class CredentialsValidatorTests
{
    private readonly CredentialsValidator _validator = new();

    [Theory]
    [InlineData("user@example.com", "password123", true)]
    [InlineData("not-an-email", "password123", false)]
    [InlineData("user@example.com", "short", false)]
    [InlineData("", "password123", false)]
    public void Validates(string email, string password, bool expected)
    {
        var result = _validator.Validate(new Credentials(email, password));
        Assert.Equal(expected, result.IsValid);
    }
}
```
- [ ] **Step 2: Run test — expect FAIL** (`not-an-email` and `short` currently pass).
- [ ] **Step 3: Implement:**
```csharp
public CredentialsValidator()
{
    RuleFor(credentials => credentials.Email).NotEmpty().EmailAddress();
    RuleFor(credentials => credentials.Password).NotEmpty().MinimumLength(8);
}
```
- [ ] **Step 4: Run test — expect PASS.**
- [ ] **Step 5: Commit.**
```bash
git add -A && git commit -m "feat: validate email format and password length in CredentialsValidator"
```

---

### Task 7: WebApiStartup — remove throwaway service provider (refactor, build-verified)

**Files:**
- Modify: `src/Configuration/WebApiStartup.cs` (`LoadConfiguration`)

**Interfaces:**
- Produces: `ConfigurationLoader` constructed without `BuildServiceProvider()`.

> Not unit-tested: `WebApiStartup` is abstract and bound to `WebApplicationBuilder`; verified by build + existing suite. Behavior-preserving.

- [ ] **Step 1: Refactor.** In `LoadConfiguration`, remove the `using var provider = Builder.Services.BuildServiceProvider();` block and the `provider.GetRequiredService<ConfigurationLoader>()` call. Construct the loader directly with a concrete logger:
```csharp
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var configurationLoader = new ConfigurationLoader(
    Builder.Configuration, Builder.Environment.EnvironmentName, null,
    loggerFactory.CreateLogger<ConfigurationLoader>());
```
Keep the existing `AddSingleton(sp => new ConfigurationLoader(...))` registration for downstream DI, and the subsequent `LoadAppSettings()` / `LoadEnvironment()` calls on `configurationLoader`.
- [ ] **Step 2: Build — expect 0 errors.**
- [ ] **Step 3: Test — expect all green.**
- [ ] **Step 4: Commit.**
```bash
git add -A && git commit -m "refactor: avoid throwaway ServiceProvider in WebApiStartup.LoadConfiguration"
```

---

### Task 8: XML documentation across the public surface

**Files (add `///` docs to every public type/member):**
- `src/Configuration/WebApiStartup.cs`, `WebApiParameters.cs`, `AppSettingsKeys.cs`
- `src/Client/BaseWebApiClient.cs`, `BaseWebApiClientRoute.cs`
- `src/AspNetCore/ResponseResolver.cs`
- `src/Middleware/WebApiMiddleware.cs`, `ExceptionMiddleware.cs`, `TraceActivityMiddleware.cs`
- `src/Handlers/TracePropagationHandler.cs` (type has docs; add to members)
- `src/Security/Attributes/*.cs`, `src/Security/Filters/RoleRequirementFilter.cs`
- `src/Security/Interfaces/IAuthenticationProvider.cs`, `src/Security/Records/*.cs`, `src/Security/Validation/CredentialsValidator.cs`, `src/Security/Extensions/AuthenticationExtensions.cs`

**Interfaces:** no code behavior change; documentation only.

- [ ] **Step 1: Enumerate the gaps.** Run the build command and capture CS1591 lines: `dotnet build src/ArturRios.Util.WebApi.csproj --nologo --no-incremental 2>&1 | rg CS1591`. This is the authoritative worklist.
- [ ] **Step 2: Document each member.** Add concise `<summary>` (and `<param>`/`<returns>`/`<typeparam>` where they add value) to every listed member. Match the voice of the already-documented Security files (e.g. `JwtMiddleware`, `CachedAuthenticationProvider`). Example for `ResponseResolver.Resolve`:
```csharp
/// <summary>Wraps a <see cref="DataOutput{T}"/> in an <see cref="ActionResult{TValue}"/>, defaulting the
/// HTTP status to 200 on success and 400 on failure unless <paramref name="statusCode"/> is supplied.</summary>
/// <param name="dataOutput">The result envelope to return.</param>
/// <param name="statusCode">Optional explicit HTTP status code.</param>
```
- [ ] **Step 3: Rebuild and confirm.** `dotnet build src/ArturRios.Util.WebApi.csproj --nologo --no-incremental 2>&1 | rg -c CS1591` → expect `0` (or justify any residual).
- [ ] **Step 4: Test — expect all green.**
- [ ] **Step 5: Commit.**
```bash
git add -A && git commit -m "docs: add XML documentation across the public API surface"
```

---

### Task 9: Version bump to 2.0.0

**Files:** Modify `src/ArturRios.Util.WebApi.csproj`.

- [ ] **Step 1:** Change `<Version>1.0.0</Version>` → `<Version>2.0.0</Version>`.
- [ ] **Step 2: Build — expect 0 errors.**
- [ ] **Step 3: Commit.**
```bash
git add -A && git commit -m "chore: bump version to 2.0.0"
```

---

### Task 10: README overhaul

**Files:** Modify `README.md`.

**Interfaces:** authored content; verified by review + link check.

- [ ] **Step 1: Rewrite `README.md`** with these sections (keep the existing Authentication content, fold it into Security):
  1. Title + one-paragraph intro (utilities for building .NET web APIs: bootstrap, security, middleware, HTTP client, response envelopes).
  2. **Install** — `dotnet add package ArturRios.Util.WebApi`; requires .NET 10.
  3. **Feature overview** table: Configuration/bootstrap, Security (JWT + roles), Middleware & diagnostics, HTTP client, Responses — one line each + a link to the matching docs page.
  4. **Quick start** — short snippet per area: deriving `WebApiStartup`, registering `JwtMiddleware` + `JwtAuthenticationOptions`, `[Authorize]`/`[RoleRequirement]`, `ResponseResolver.Resolve(...)`, a `BaseWebApiClient` subclass.
  5. **Documentation** link (`https://artur-rios.github.io/dotnet-webapi-util`), **Versioning** (keep existing SemVer note; mention 2.0 namespace change), **Legal** (keep).
- [ ] **Step 2:** Verify all links resolve (relative doc links + NuGet + docs site).
- [ ] **Step 3: Commit.**
```bash
git add README.md && git commit -m "docs: rewrite README as a full feature overview"
```

---

### Task 11: Hugo site config + landing page

**Files:** Modify `docs/hugo.toml`; create `docs/content/_index.md`.

- [ ] **Step 1: Rewrite `docs/hugo.toml`** modeled on `dotnet-data/docs/hugo.toml`: `baseURL = 'https://artur-rios.github.io/dotnet-webapi-util'`, `locale='en-us'`, `title='Dotnet WebApi Util'`, `theme='hugo-theme-re-terminal'`, `[params]` (`author_email`, `name`, `[params.logo] logoText`), and `[menu]` entries: Architecture (10), Configuration (20), Security (30), Middleware & Diagnostics (40), HTTP Client (50), Responses (60), Author → `https://www.artur-rios.tech/` (70), GitHub → repo (80). Omit the coverage-report menu item.
- [ ] **Step 2: Write `docs/content/_index.md`** (TOML `+++` front-matter, `title='Dotnet WebApi Util'`): intro paragraph; a mermaid `flowchart` of the request pipeline (client → TraceActivity → Exception → Jwt → endpoint) and the feature areas; the feature table; an Install section (.NET 10, `dotnet add package ArturRios.Util.WebApi`); a short note on the `ArturRios.Output` envelopes returned by `ResponseResolver`; a "Where to next" list linking each feature page. Mirror the tone of `dotnet-data/docs/content/_index.md`.
- [ ] **Step 3: Verify** front-matter parses and internal links (`/architecture/`, `/security/`, …) match menu URLs.
- [ ] **Step 4: Commit.**
```bash
git add docs/hugo.toml docs/content/_index.md && git commit -m "docs(site): configure Hugo and add landing page"
```

---

### Task 12: Hugo — architecture page

**Files:** Create `docs/content/architecture.md`.

- [ ] **Step 1: Write `architecture.md`** (`+++ title='Architecture' +++`): 
  - **Request pipeline** — mermaid flow of middleware order (`TraceActivityMiddleware` → `ExceptionMiddleware` → `JwtMiddleware` → MVC/endpoint), explaining `WebApiMiddleware` marker + `WebApiStartup.AddMiddlewares` registration.
  - **Security model** — mermaid: token → `JwtMiddleware` (`ClaimsOnly` vs `Revalidate`) → `HttpContext.Items["User"]` → `AuthorizeAttribute`/`RoleRequirementFilter`. Note the `CachedAuthenticationProvider` seam.
  - **Response envelopes** — mermaid class diagram `ProcessOutput <|-- DataOutput<T>`, and `ResponseResolver` status mapping.
  - **Design principles** — envelopes-not-exceptions, stateless-by-default auth, small focused middleware, consistent `InvokeAsync`, DI-first.
- [ ] **Step 2: Verify** mermaid fences and links.
- [ ] **Step 3: Commit.**
```bash
git add docs/content/architecture.md && git commit -m "docs(site): add architecture page"
```

---

### Task 13: Hugo — Configuration page

**Files:** Create `docs/content/configuration.md`.

- [ ] **Step 1: Write** `configuration.md` (`title='Configuration'`) covering: deriving `WebApiStartup` and its lifecycle hooks (`Build`, `ConfigureApp`, `AddDependencies`, `ConfigureCors`, `ConfigureSecurity`, `ConfigureWebApi`, `StartServices`, `LoadConfiguration`, `AddMiddlewares`, `AddCustomLogging`, `AddCustomInvalidModelStateResponse`, `UseSwagger`/`UseSwaggerGen`); `WebApiParameters` CLI arg format (`Environment:Development`, `UseAppSetting`, `UseEnvFile`, `EnableSwaggerDocs`, `SwaggerEnvironments:[...]`); `AppSettingsKeys.SwaggerEnabled`. Include a minimal `class Startup : WebApiStartup` example.
- [ ] **Step 2: Commit.**
```bash
git add docs/content/configuration.md && git commit -m "docs(site): add configuration page"
```

---

### Task 14: Hugo — Security page

**Files:** Create `docs/content/security.md`.

- [ ] **Step 1: Write** `security.md` (`title='Security'`) covering: `JwtMiddleware` + `JwtAuthenticationOptions.ValidationMode` (`ClaimsOnly` default vs `Revalidate`) with the trade-off (short-lived tokens/refresh); `IAuthenticationProvider` and `CachedAuthenticationProvider` via `AddCachedAuthenticationProvider<T>` (`Ttl`, `CacheMisses`); `AuthenticatedUser`, `TokenClaimKeys`, `AuthenticationExtensions.ToTokenClaims`, `AuthenticatedUserFactory.FromToken`; `[Authorize]`, `[RoleRequirement(...)]` + `RoleRequirementFilter`, `[AllowAnonymous]`; `Credentials` + `CredentialsValidator` rules; `Authentication` record. Reuse/expand the README Authentication content. Include the `Revalidate` + caching code snippets.
- [ ] **Step 2: Commit.**
```bash
git add docs/content/security.md && git commit -m "docs(site): add security page"
```

---

### Task 15: Hugo — Middleware & Diagnostics page

**Files:** Create `docs/content/middleware-and-diagnostics.md`.

- [ ] **Step 1: Write** `middleware-and-diagnostics.md` (`title='Middleware & Diagnostics'`) covering: the `WebApiMiddleware` marker + `AddMiddlewares(Type[])` registration order; `ExceptionMiddleware` (envelope response, generic message vs `CustomException`, client-cancellation handling, structured logging); `TraceActivityMiddleware` (W3C trace ids, `TraceId` item, `traceparent` response header); `TracePropagationHandler` (outgoing `traceparent` on typed/named `HttpClient`s). Include a registration snippet.
- [ ] **Step 2: Commit.**
```bash
git add docs/content/middleware-and-diagnostics.md && git commit -m "docs(site): add middleware & diagnostics page"
```

---

### Task 16: Hugo — HTTP Client page

**Files:** Create `docs/content/http-client.md`.

- [ ] **Step 1: Write** `http-client.md` (`title='HTTP Client'`) covering: `BaseWebApiClient` (both ctors — `HttpClient` vs base URL; recommend the `HttpClient`/`IHttpClientFactory` path), `SetRoutes`; `BaseWebApiClientRoute` (`BaseUrl`, `AuthenticateAsync`, idempotent `Authorize`, `AuthenticateAndAuthorizeAsync`, `WebApiClientException`); pairing with `TracePropagationHandler`. Include a concrete client + route subclass example.
- [ ] **Step 2: Commit.**
```bash
git add docs/content/http-client.md && git commit -m "docs(site): add HTTP client page"
```

---

### Task 17: Hugo — Responses page

**Files:** Create `docs/content/responses.md`.

- [ ] **Step 1: Write** `responses.md` (`title='Responses'`) covering: `ResponseResolver.Resolve` overloads for `DataOutput<T>`, `ProcessOutput`, `PaginatedOutput<T>`; default status mapping (200 success / 400 failure) and the `statusCode` override; how it pairs with the `ArturRios.Output` envelopes. Include a controller-action example returning `ResponseResolver.Resolve(output)`.
- [ ] **Step 2: Commit.**
```bash
git add docs/content/responses.md && git commit -m "docs(site): add responses page"
```

---

### Task 18: Final verification

**Files:** none (verification only).

- [ ] **Step 1: Full build.** `dotnet build src/ArturRios.Util.WebApi.csproj --nologo --no-incremental` → 0 errors; `rg -c CS1591` over the output → 0.
- [ ] **Step 2: Full test.** `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --nologo -v q` → all green.
- [ ] **Step 3: Hugo check.** If `hugo` is installed: `cd docs && hugo --gc --minify` → builds with no errors. If not installed: verify every `content/*.md` has valid `+++` front-matter, and every `[menu]` URL has a matching page. Note which check was used.
- [ ] **Step 4: Spec coverage pass.** Re-read the design spec; confirm each work item A–H maps to a completed task.
- [ ] **Step 5: Final commit (if anything adjusted).**
```bash
git add -A && git commit -m "chore: final verification for 2.0.0 hardening & docs"
```

---

## Self-Review

- **Spec coverage:** A (Task 1) · B1 (Task 2) · B2 (Task 3) · B3–B4 (Task 4) · B5 (Task 7) · B6 (Task 2) · B7 (Task 5) · B8 (Task 6) · C (Tasks 3–5, 8) · D (Task 8) · E (Task 10) · F (Tasks 11–17) · G (Task 18) · H (Task 9). All covered.
- **Placeholder scan:** none — every code step shows code; docs steps enumerate exact front-matter, headings, and coverage.
- **Type consistency:** `WebApiClientException`, `ExtractBearerToken`, `InvokeAsync`, `AddCachedAuthenticationProvider<T>`, `CredentialsValidator` rules referenced consistently across tasks.
- **Note:** Task 7 is a build-verified refactor (WebApiStartup is not unit-testable without a host) — explicitly flagged, behavior-preserving.
