# Authentication Token Sources + Google Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an app read the auth token from the `Authorization` header, a cookie, or either, and validate it as **either** an app HMAC JWT **or** a Google ID token — resolving the app user through `IAuthenticationProvider`.

**Architecture:** Replace the single-purpose `JwtMiddleware` with an `AuthenticationMiddleware` that extracts one token (via `TokenExtractor` + a configurable `TokenSource`) and runs it through the enabled `ITokenValidator`s in order (`JwtTokenValidator` first, `GoogleTokenValidator` second); the first validator that yields a user wins. Google validation sits behind an `IGoogleTokenVerifier` seam so it is unit-testable without network. One consolidated `AuthenticationOptions` configures everything.

**Tech Stack:** .NET 10, xUnit, `Microsoft.Extensions.Logging`, `ArturRios.Jwt` (`JwtHandler`/`JwtConfiguration`), `Google.Apis.Auth`.

## Global Constraints

- Target framework `net10.0`; do not change.
- Spec: `docs/superpowers/specs/2026-07-22-auth-sources-and-google-design.md`.
- Run `dotnet` commands from repo root `D:\Repositories\dotnet-webapi-util`; build with `dotnet build src`, test with `dotnet test tests`.
- **Convention-based middleware is a singleton:** the middleware and all `ITokenValidator`s are resolved from the root provider, so they MUST NOT take request-scoped services (like `IAuthenticationProvider`) in their constructors. Resolve `IAuthenticationProvider` from `context.RequestServices.GetRequiredService<IAuthenticationProvider>()` inside `ValidateAsync`, exactly as today's `JwtMiddleware.ResolveUser` does.
- Follow existing folder/namespace conventions under `src/Security/...`. New auth building blocks (`TokenExtractor`, validators, verifier) go in a new folder `src/Security/Authentication/` with namespace `ArturRios.Util.WebApi.Security.Authentication`.
- `AuthenticatedUser(int Id, int Role)` shape does not change.
- `JwtValidationMode` is retained. `JwtAuthenticationOptions` is removed (folded into `AuthenticationOptions`) — but only in Task 7, after the middleware stops using it.
- 401 responses keep the existing shape: `ProcessOutput.New.WithError(message)` serialized with `Newtonsoft.Json`, status 401, content-type `application/json`.
- TDD: write the failing test first for every behavioral unit (Tasks 1, 4, 5, 6, 7). Run the full suite once before each commit. Commit after each task.

## File Structure

- `src/Security/Enums/TokenSource.cs` — new enum (Task 1).
- `src/Security/Authentication/TokenExtractor.cs` — new, static extractor (Task 1).
- `src/Security/Interfaces/ITokenValidator.cs` — new interface (Task 2).
- `src/Security/Records/TokenValidationResult.cs` — new result struct (Task 2).
- `src/Security/Configuration/AuthenticationOptions.cs` — new consolidated options (Task 3).
- `src/Security/Authentication/JwtTokenValidator.cs` — new, wraps HMAC logic (Task 4).
- `src/Security/Interfaces/IAuthenticationProvider.cs` — add `GetAuthenticatedUserByEmail` (Task 5).
- `src/Security/Providers/CachedAuthenticationProvider.cs` — implement the new method (Task 5).
- `src/Security/Interfaces/IGoogleTokenVerifier.cs` + `src/Security/Records/GoogleTokenPayload.cs` — new seam (Task 6).
- `src/Security/Authentication/GoogleTokenVerifier.cs` — new, wraps `Google.Apis.Auth` (Task 6).
- `src/Security/Authentication/GoogleTokenValidator.cs` — new (Task 6).
- `src/Security/Middleware/AuthenticationMiddleware.cs` — renamed from `JwtMiddleware.cs`, rewritten (Task 7).
- `src/Security/Configuration/JwtAuthenticationOptions.cs` — deleted (Task 7).
- `src/Security/Extensions/AuthenticationServiceCollectionExtensions.cs` — add `AddTokenAuthentication` (Task 7).
- `tests/Security/*` — new/updated test files per task.
- Docs (Task 8): `README.md`, `docs/content/configuration.md`, `docs/content/security.md`.

---

### Task 1: `TokenSource` enum + `TokenExtractor`

**Files:**
- Create: `src/Security/Enums/TokenSource.cs`
- Create: `src/Security/Authentication/TokenExtractor.cs`
- Test: `tests/Security/TokenExtractorTests.cs`

**Interfaces:**
- Produces: `enum TokenSource { Header, Cookie, Either }`; `static class TokenExtractor` with `static string Extract(HttpContext context, TokenSource source, string cookieName)` returning the raw token or `string.Empty`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Security/TokenExtractorTests.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Enums;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Security;

public class TokenExtractorTests
{
    private static DefaultHttpContext ContextWith(string? header, string? cookieName = null, string? cookieValue = null)
    {
        var context = new DefaultHttpContext();

        if (header is not null)
        {
            context.Request.Headers.Authorization = header;
        }

        if (cookieName is not null && cookieValue is not null)
        {
            context.Request.Headers.Cookie = $"{cookieName}={cookieValue}";
        }

        return context;
    }

    [Fact]
    public void Header_ReturnsBearerToken()
    {
        var context = ContextWith("Bearer abc.def.ghi");
        Assert.Equal("abc.def.ghi", TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Header_AcceptsLowercaseScheme()
    {
        var context = ContextWith("bearer abc.def.ghi");
        Assert.Equal("abc.def.ghi", TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Header_ReturnsEmpty_ForNonBearerScheme()
    {
        var context = ContextWith("Basic abc");
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Header_ReturnsEmpty_WhenAbsent()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Cookie_ReturnsNamedCookie()
    {
        var context = ContextWith(header: null, cookieName: "access_token", cookieValue: "cookie.token.value");
        Assert.Equal("cookie.token.value", TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void Cookie_ReturnsEmpty_WhenAbsent()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void Cookie_IgnoresHeader()
    {
        var context = ContextWith("Bearer header.token", cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("cookie.token", TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void Either_PrefersHeader()
    {
        var context = ContextWith("Bearer header.token", cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("header.token", TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }

    [Fact]
    public void Either_FallsBackToCookie_WhenHeaderMissing()
    {
        var context = ContextWith(header: null, cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("cookie.token", TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }

    [Fact]
    public void Either_ReturnsEmpty_WhenNeitherPresent()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests`
Expected: FAIL — `TokenSource`/`TokenExtractor` do not exist (compile error).

- [ ] **Step 3: Create the enum**

`src/Security/Enums/TokenSource.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Enums;

/// <summary>Where <c>AuthenticationMiddleware</c> reads the raw authentication token from.</summary>
public enum TokenSource
{
    /// <summary>Read the token from the <c>Authorization: Bearer &lt;token&gt;</c> header only.</summary>
    Header,

    /// <summary>Read the token from the configured cookie only.</summary>
    Cookie,

    /// <summary>Read the token from the header first, then fall back to the cookie.</summary>
    Either
}
```

- [ ] **Step 4: Create the extractor**

`src/Security/Authentication/TokenExtractor.cs`:

```csharp
using System.Net.Http.Headers;
using ArturRios.Util.WebApi.Security.Enums;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Reads the raw authentication token from an <see cref="HttpContext"/> according to a <see cref="TokenSource"/>.</summary>
public static class TokenExtractor
{
    /// <summary>Extracts the token from the header, the named cookie, or either (header first), returning <see cref="string.Empty"/> when none is found.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="source">Where to read the token from.</param>
    /// <param name="cookieName">The cookie name used when <paramref name="source"/> is <see cref="TokenSource.Cookie"/> or <see cref="TokenSource.Either"/>.</param>
    public static string Extract(HttpContext context, TokenSource source, string cookieName)
    {
        ArgumentNullException.ThrowIfNull(context);

        return source switch
        {
            TokenSource.Header => FromHeader(context),
            TokenSource.Cookie => FromCookie(context, cookieName),
            TokenSource.Either => FromHeader(context) is { Length: > 0 } header ? header : FromCookie(context, cookieName),
            _ => string.Empty
        };
    }

    private static string FromHeader(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header) || !AuthenticationHeaderValue.TryParse(header, out var parsed) ||
            !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return string.Empty;
        }

        return parsed.Parameter.Trim();
    }

    private static string FromCookie(HttpContext context, string cookieName)
    {
        var value = context.Request.Cookies[cookieName];

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests`
Expected: PASS — all `TokenExtractorTests` green, plus the existing suite still green.

- [ ] **Step 6: Commit**

```bash
git add src/Security/Enums/TokenSource.cs src/Security/Authentication/TokenExtractor.cs tests/Security/TokenExtractorTests.cs
git commit -m "feat: add TokenSource and TokenExtractor"
```

---

### Task 2: `ITokenValidator` + `TokenValidationResult`

**Files:**
- Create: `src/Security/Interfaces/ITokenValidator.cs`
- Create: `src/Security/Records/TokenValidationResult.cs`

**Interfaces:**
- Produces: `readonly record struct TokenValidationResult(AuthenticatedUser? User, string? Error)`; `interface ITokenValidator { Task<TokenValidationResult> ValidateAsync(string token, HttpContext context); }`. Consumed by Tasks 4, 5, 7.

**Note:** These are contract-only types with no behavior, so there is no failing test to write here. The compiler (build) is the check; behavioral tests arrive with the implementations in Tasks 4–5.

- [ ] **Step 1: Create the result struct**

`src/Security/Records/TokenValidationResult.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The outcome of an <c>ITokenValidator</c> attempt: a resolved user, or an error describing why validation failed.</summary>
/// <param name="User">The authenticated user when validation succeeded; otherwise <see langword="null"/>.</param>
/// <param name="Error">A human-readable error when <paramref name="User"/> is <see langword="null"/>; otherwise <see langword="null"/>.</param>
public readonly record struct TokenValidationResult(AuthenticatedUser? User, string? Error);
```

- [ ] **Step 2: Create the interface**

`src/Security/Interfaces/ITokenValidator.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Validates a raw authentication token and, on success, resolves the <see cref="Records.AuthenticatedUser"/> it represents.</summary>
public interface ITokenValidator
{
    /// <summary>Attempts to validate <paramref name="token"/> and resolve its user. Returns a result whose <c>User</c> is
    /// <see langword="null"/> (with an <c>Error</c>) when this validator does not accept the token, so the caller can try the next validator.</summary>
    /// <param name="token">The raw token extracted from the request. May be empty.</param>
    /// <param name="context">The current HTTP context, used to resolve request-scoped services such as <c>IAuthenticationProvider</c>.</param>
    Task<TokenValidationResult> ValidateAsync(string token, HttpContext context);
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Security/Interfaces/ITokenValidator.cs src/Security/Records/TokenValidationResult.cs
git commit -m "feat: add ITokenValidator and TokenValidationResult contracts"
```

---

### Task 3: `AuthenticationOptions`

**Files:**
- Create: `src/Security/Configuration/AuthenticationOptions.cs`

**Interfaces:**
- Produces: `class AuthenticationOptions` with `TokenSource Source = Header`, `string CookieName = "access_token"`, `bool EnableJwt = true`, `bool EnableGoogle = false`, `JwtValidationMode JwtMode = ClaimsOnly`, `IList<string> GoogleClientIds = new List<string>()`. Consumed by Tasks 4, 5, 7.

**Note:** `JwtAuthenticationOptions` is NOT removed here — the current `JwtMiddleware` still references it and must keep compiling until Task 7. This task only adds the new type alongside it. Contract-only; build is the check.

- [ ] **Step 1: Create the options class**

`src/Security/Configuration/AuthenticationOptions.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Enums;

namespace ArturRios.Util.WebApi.Security.Configuration;

/// <summary>Consolidated options controlling how <c>AuthenticationMiddleware</c> reads and validates the request token.</summary>
public class AuthenticationOptions
{
    /// <summary>Where the token is read from. Defaults to <see cref="TokenSource.Header"/>.</summary>
    public TokenSource Source { get; set; } = TokenSource.Header;

    /// <summary>The cookie name used when <see cref="Source"/> is <see cref="TokenSource.Cookie"/> or <see cref="TokenSource.Either"/>. Defaults to <c>access_token</c>.</summary>
    public string CookieName { get; set; } = "access_token";

    /// <summary>Whether the app's own HMAC JWT is accepted. Defaults to <see langword="true"/>.</summary>
    public bool EnableJwt { get; set; } = true;

    /// <summary>Whether Google ID tokens are accepted. Defaults to <see langword="false"/>. Requires <see cref="GoogleClientIds"/> and a registered <c>IAuthenticationProvider</c>.</summary>
    public bool EnableGoogle { get; set; }

    /// <summary>How the user is resolved for a valid app JWT. Defaults to <see cref="JwtValidationMode.ClaimsOnly"/>.</summary>
    public JwtValidationMode JwtMode { get; set; } = JwtValidationMode.ClaimsOnly;

    /// <summary>The accepted Google OAuth client IDs (token audiences). Required when <see cref="EnableGoogle"/> is <see langword="true"/>.</summary>
    public IList<string> GoogleClientIds { get; set; } = new List<string>();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Security/Configuration/AuthenticationOptions.cs
git commit -m "feat: add consolidated AuthenticationOptions"
```

---

### Task 4: `JwtTokenValidator`

**Files:**
- Create: `src/Security/Authentication/JwtTokenValidator.cs`
- Test: `tests/Security/JwtTokenValidatorTests.cs`

**Interfaces:**
- Consumes: `ITokenValidator`, `TokenValidationResult` (Task 2); `AuthenticationOptions` (Task 3); existing `JwtConfiguration`, `JwtHandler` (from `ArturRios.Jwt`), `AuthenticatedUserFactory`, `IAuthenticationProvider`, `JwtValidationMode`.
- Produces: `class JwtTokenValidator(JwtConfiguration jwtConfig, JwtHandler jwtHandler, AuthenticationOptions options) : ITokenValidator`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Security/JwtTokenValidatorTests.cs`:

```csharp
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class JwtTokenValidatorTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private sealed class StubProvider(AuthenticatedUser? byId) : IAuthenticationProvider
    {
        public AuthenticatedUser? GetAuthenticatedUserById(int id) => byId;
        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    private static JwtConfiguration Config() => new(3600, "issuer", "audience", Secret, new Dictionary<string, string>());

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    private static HttpContext ContextWithProvider(IAuthenticationProvider? provider)
    {
        var context = new DefaultHttpContext();

        if (provider is not null)
        {
            context.RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider();
        }

        return context;
    }

    private static JwtTokenValidator Validator(JwtValidationMode mode) =>
        new(Config(), new JwtHandler(), new AuthenticationOptions { JwtMode = mode });

    [Fact]
    public async Task ClaimsOnly_ReturnsUserFromClaims()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
        Assert.Equal(42, result.User!.Id);
        Assert.Equal(3, result.User.Role);
    }

    [Fact]
    public async Task InvalidSignature_ReturnsError()
    {
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync("not-a-token", ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task Revalidate_ResolvesUserFromProvider()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var provider = new StubProvider(new AuthenticatedUser(42, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.NotNull(result.User);
        Assert.Equal(9, result.User!.Role);
    }

    [Fact]
    public async Task Revalidate_ReturnsUserNotFound_WhenProviderReturnsNull()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests`
Expected: FAIL — `JwtTokenValidator` does not exist.

- [ ] **Step 3: Implement the validator**

`src/Security/Authentication/JwtTokenValidator.cs`:

```csharp
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Factories;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Validates the app's own HMAC-signed JWT and resolves the user by claims or by an <see cref="IAuthenticationProvider"/> lookup, per <see cref="AuthenticationOptions.JwtMode"/>.</summary>
/// <param name="jwtConfig">Provides the signing secret used to validate the token.</param>
/// <param name="jwtHandler">Validates signatures and reads the user id claim.</param>
/// <param name="options">Controls how the user is resolved once the signature is valid.</param>
public class JwtTokenValidator(JwtConfiguration jwtConfig, JwtHandler jwtHandler, AuthenticationOptions options) : ITokenValidator
{
    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        var isValid = await jwtHandler.IsTokenValidAsync(token, jwtConfig.Secret);

        if (!isValid)
        {
            return new TokenValidationResult(null, "Invalid token");
        }

        if (options.JwtMode == JwtValidationMode.ClaimsOnly)
        {
            var claimsUser = AuthenticatedUserFactory.FromToken(token);

            return new TokenValidationResult(claimsUser, claimsUser is null ? "Could not retrieve user from token" : null);
        }

        var userId = jwtHandler.GetUserIdFromToken(token);

        if (!userId.HasValue)
        {
            return new TokenValidationResult(null, "Could not retrieve user id from token");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserById(userId.Value);

        return new TokenValidationResult(user, user is null ? "User not found" : null);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests`
Expected: PASS — new `JwtTokenValidatorTests` green. (The existing `JwtMiddlewareTests` still compile: `IAuthenticationProvider` gains its second method in Task 5; the stub above declares both methods locally, so it compiles now.)

- [ ] **Step 5: Commit**

```bash
git add src/Security/Authentication/JwtTokenValidator.cs tests/Security/JwtTokenValidatorTests.cs
git commit -m "feat: add JwtTokenValidator"
```

---

### Task 6: Google verification seam + `GoogleTokenValidator`

> **Dispatch order:** this task depends on `IAuthenticationProvider.GetAuthenticatedUserByEmail`, which is added in **Task 5** (the section immediately below). Tasks are executed by number, so Task 5 is implemented first and this compiles.

**Files:**
- Modify: `src/ArturRios.Util.WebApi.csproj` (add `Google.Apis.Auth`)
- Create: `src/Security/Records/GoogleTokenPayload.cs`
- Create: `src/Security/Interfaces/IGoogleTokenVerifier.cs`
- Create: `src/Security/Authentication/GoogleTokenVerifier.cs`
- Create: `src/Security/Authentication/GoogleTokenValidator.cs`
- Test: `tests/Security/GoogleTokenValidatorTests.cs`

**Interfaces:**
- Consumes: `ITokenValidator`, `TokenValidationResult` (Task 2); `AuthenticationOptions` (Task 3); `IAuthenticationProvider.GetAuthenticatedUserByEmail` (added in Task 5, which is implemented before this task).
- Produces: `record GoogleTokenPayload(string Email, string Subject)`; `interface IGoogleTokenVerifier { Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences); }`; `class GoogleTokenVerifier : IGoogleTokenVerifier`; `class GoogleTokenValidator(IGoogleTokenVerifier verifier, AuthenticationOptions options) : ITokenValidator`.

- [ ] **Step 1: Add the `Google.Apis.Auth` package reference**

In `src/ArturRios.Util.WebApi.csproj`, add to the package `<ItemGroup>` (alphabetical among the `Google`/`Microsoft` entries is fine):

```xml
<PackageReference Include="Google.Apis.Auth" Version="1.72.0" />
```

Then run: `dotnet restore src` and `dotnet build src` — expected `Build succeeded`. If version `1.72.0` fails to resolve, run `dotnet add src package Google.Apis.Auth` to pick the current stable and note the resolved version in your report.

- [ ] **Step 2: Write the failing tests**

Create `tests/Security/GoogleTokenValidatorTests.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class GoogleTokenValidatorTests
{
    private sealed class FakeVerifier(GoogleTokenPayload? payload) : IGoogleTokenVerifier
    {
        public Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences) => Task.FromResult(payload);
    }

    private sealed class StubProvider(AuthenticatedUser? byEmail) : IAuthenticationProvider
    {
        public AuthenticatedUser? GetAuthenticatedUserById(int id) => null;
        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }

    private static HttpContext ContextWithProvider(IAuthenticationProvider provider) =>
        new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider() };

    private static AuthenticationOptions Options() =>
        new() { EnableGoogle = true, GoogleClientIds = { "client-id-1" } };

    [Fact]
    public async Task ValidToken_ResolvesUserByEmail()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("user@example.com", "google-sub-1"));
        var provider = new StubProvider(new AuthenticatedUser(7, 2));
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(provider));

        Assert.NotNull(result.User);
        Assert.Equal(7, result.User!.Id);
    }

    [Fact]
    public async Task ValidToken_UnknownEmail_ReturnsUserNotFound()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("nobody@example.com", "google-sub-2"));
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }

    [Fact]
    public async Task RejectedToken_ReturnsError()
    {
        var verifier = new FakeVerifier(payload: null); // verifier rejected audience/issuer/expiry/signature
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("bad.token", ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests`
Expected: FAIL — `GoogleTokenPayload`/`IGoogleTokenVerifier`/`GoogleTokenValidator` do not exist.

- [ ] **Step 4: Create the payload record**

`src/Security/Records/GoogleTokenPayload.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The verified subset of a Google ID token used to resolve the app user.</summary>
/// <param name="Email">The token's verified email claim.</param>
/// <param name="Subject">Google's stable subject identifier (<c>sub</c>).</param>
public record GoogleTokenPayload(string Email, string Subject);
```

- [ ] **Step 5: Create the verifier seam**

`src/Security/Interfaces/IGoogleTokenVerifier.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Verifies a Google ID token against the accepted audiences and returns its verified payload, or <see langword="null"/> when the token is not valid.</summary>
public interface IGoogleTokenVerifier
{
    /// <summary>Verifies <paramref name="token"/> (signature, issuer, expiry, and audience against <paramref name="audiences"/>).</summary>
    /// <returns>The verified payload, or <see langword="null"/> if the token is invalid or rejected.</returns>
    Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences);
}
```

- [ ] **Step 6: Create the default verifier wrapping `Google.Apis.Auth`**

`src/Security/Authentication/GoogleTokenVerifier.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Google.Apis.Auth;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Default <see cref="IGoogleTokenVerifier"/> backed by <see cref="GoogleJsonWebSignature"/>, which fetches and caches Google's signing keys and checks signature, issuer, expiry, and audience.</summary>
public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    /// <inheritdoc />
    public async Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings { Audience = audiences };
            var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);

            return new GoogleTokenPayload(payload.Email ?? string.Empty, payload.Subject ?? string.Empty);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 7: Create the validator**

`src/Security/Authentication/GoogleTokenValidator.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Validates a Google ID token via <see cref="IGoogleTokenVerifier"/> and resolves the app user by the token's verified email through <see cref="IAuthenticationProvider"/>.</summary>
/// <param name="verifier">Verifies the Google ID token against the configured client IDs.</param>
/// <param name="options">Supplies the accepted Google client IDs (audiences).</param>
public class GoogleTokenValidator(IGoogleTokenVerifier verifier, AuthenticationOptions options) : ITokenValidator
{
    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        var payload = await verifier.VerifyAsync(token, options.GoogleClientIds);

        if (payload is null)
        {
            return new TokenValidationResult(null, "Invalid Google token");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return new TokenValidationResult(null, "Google token has no email");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserByEmail(payload.Email);

        return new TokenValidationResult(user, user is null ? "User not found" : null);
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests`
Expected: PASS — `GoogleTokenValidatorTests` green, existing suite still green.

- [ ] **Step 9: Commit**

```bash
git add src/ArturRios.Util.WebApi.csproj src/Security/Records/GoogleTokenPayload.cs src/Security/Interfaces/IGoogleTokenVerifier.cs src/Security/Authentication/GoogleTokenVerifier.cs src/Security/Authentication/GoogleTokenValidator.cs tests/Security/GoogleTokenValidatorTests.cs
git commit -m "feat: add Google ID token verification and validator"
```

---

### Task 5: `IAuthenticationProvider.GetAuthenticatedUserByEmail` + caching

**Files:**
- Modify: `src/Security/Interfaces/IAuthenticationProvider.cs`
- Modify: `src/Security/Providers/CachedAuthenticationProvider.cs`
- Modify: `src/Security/Providers/CachedAuthenticationProviderOptions.cs` (add an email cache-key prefix)
- Modify: `tests/Security/CachedAuthenticationProviderTests.cs` (add email-lookup coverage; update any local `IAuthenticationProvider` stubs to add the new method)
- Modify: `tests/Security/JwtMiddlewareTests.cs` (its `CountingAuthenticationProvider` implements `IAuthenticationProvider`; add the new method so it compiles)

**Interfaces:**
- Produces: `AuthenticatedUser? GetAuthenticatedUserByEmail(string email)` on `IAuthenticationProvider`, implemented by `CachedAuthenticationProvider` with independent positive/negative caching. Consumed by `GoogleTokenValidator` (Task 6) and Task 7.

**Note:** Adding a method to `IAuthenticationProvider` breaks every implementer until updated. This task must update ALL implementers in the test project in the same commit. Search first: `grep -rln "IAuthenticationProvider" tests` and add the method to each stub/double.

- [ ] **Step 1: Write the failing test for email caching**

Add to `tests/Security/CachedAuthenticationProviderTests.cs` a counting provider that tracks email calls and tests that (a) a hit is cached (second call does not hit inner) and (b) with `CacheMisses = true`, a miss is cached, and (c) email and id caches are independent. Match the existing file's style. Concretely add:

```csharp
[Fact]
public void GetAuthenticatedUserByEmail_CachesPositiveResult()
{
    var inner = new CountingProvider(byEmail: new AuthenticatedUser(5, 1));
    var cache = new MemoryCache(new MemoryCacheOptions());
    var provider = new CachedAuthenticationProvider(inner, cache);

    var first = provider.GetAuthenticatedUserByEmail("a@b.com");
    var second = provider.GetAuthenticatedUserByEmail("a@b.com");

    Assert.Equal(5, first!.Id);
    Assert.Equal(5, second!.Id);
    Assert.Equal(1, inner.EmailCallCount);
}

[Fact]
public void GetAuthenticatedUserByEmail_CachesMiss_WhenEnabled()
{
    var inner = new CountingProvider(byEmail: null);
    var cache = new MemoryCache(new MemoryCacheOptions());
    var provider = new CachedAuthenticationProvider(inner, cache, new CachedAuthenticationProviderOptions { CacheMisses = true });

    provider.GetAuthenticatedUserByEmail("missing@b.com");
    provider.GetAuthenticatedUserByEmail("missing@b.com");

    Assert.Equal(1, inner.EmailCallCount);
}

[Fact]
public void EmailAndIdCaches_AreIndependent()
{
    var inner = new CountingProvider(byId: new AuthenticatedUser(9, 1), byEmail: new AuthenticatedUser(5, 1));
    var cache = new MemoryCache(new MemoryCacheOptions());
    var provider = new CachedAuthenticationProvider(inner, cache);

    provider.GetAuthenticatedUserById(9);
    provider.GetAuthenticatedUserByEmail("a@b.com");

    Assert.Equal(1, inner.IdCallCount);
    Assert.Equal(1, inner.EmailCallCount);
}
```

If the existing test file has no shared `CountingProvider`, add one (or extend the existing double) with this shape:

```csharp
private sealed class CountingProvider(AuthenticatedUser? byId = null, AuthenticatedUser? byEmail = null) : IAuthenticationProvider
{
    public int IdCallCount { get; private set; }
    public int EmailCallCount { get; private set; }

    public AuthenticatedUser? GetAuthenticatedUserById(int id)
    {
        IdCallCount++;
        return byId;
    }

    public AuthenticatedUser? GetAuthenticatedUserByEmail(string email)
    {
        EmailCallCount++;
        return byEmail;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests`
Expected: FAIL — `GetAuthenticatedUserByEmail` not on the interface (compile error), which also flags every implementer that needs updating.

- [ ] **Step 3: Add the interface method**

In `src/Security/Interfaces/IAuthenticationProvider.cs`, add below `GetAuthenticatedUserById`:

```csharp
    /// <summary>Looks up the authenticated user with the given email, used when resolving an external (e.g. Google) identity.</summary>
    /// <param name="email">The verified email from the external token.</param>
    /// <returns>The matching <see cref="AuthenticatedUser"/>, or <c>null</c> if none was found.</returns>
    AuthenticatedUser? GetAuthenticatedUserByEmail(string email);
```

- [ ] **Step 4: Add the email cache-key prefix option**

In `src/Security/Providers/CachedAuthenticationProviderOptions.cs`, add:

```csharp
    /// <summary>The prefix used to build the cache key for each user email. Defaults to <c>auth:email:</c>.</summary>
    public string EmailCacheKeyPrefix { get; set; } = "auth:email:";
```

- [ ] **Step 5: Implement the cached email lookup**

In `src/Security/Providers/CachedAuthenticationProvider.cs`, add (mirroring `GetAuthenticatedUserById`):

```csharp
    /// <inheritdoc />
    public AuthenticatedUser? GetAuthenticatedUserByEmail(string email)
    {
        var key = $"{_options.EmailCacheKeyPrefix}{email}";

        if (cache.TryGetValue(key, out AuthenticatedUser? cachedUser))
        {
            return cachedUser;
        }

        var user = inner.GetAuthenticatedUserByEmail(email);

        if (user is not null || _options.CacheMisses)
        {
            cache.Set(key, user, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.Ttl });
        }

        return user;
    }
```

- [ ] **Step 6: Update remaining implementers so the project compiles**

Run: `grep -rln "IAuthenticationProvider" tests` and add `public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;` (or a purpose-appropriate body) to every test double that implements the interface — at minimum `tests/Security/JwtMiddlewareTests.cs` (`CountingAuthenticationProvider`). Do not change production behavior.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests`
Expected: PASS — new caching tests green, whole suite green.

- [ ] **Step 8: Commit**

```bash
git add src/Security/Interfaces/IAuthenticationProvider.cs src/Security/Providers/CachedAuthenticationProvider.cs src/Security/Providers/CachedAuthenticationProviderOptions.cs tests/Security/CachedAuthenticationProviderTests.cs tests/Security/JwtMiddlewareTests.cs
git commit -m "feat: add email lookup to IAuthenticationProvider with caching"
```

---

### Task 7: `AuthenticationMiddleware` (rename + rewrite), registration, remove `JwtAuthenticationOptions`

**Files:**
- Rename + rewrite: `src/Security/Middleware/JwtMiddleware.cs` → `src/Security/Middleware/AuthenticationMiddleware.cs`
- Delete: `src/Security/Configuration/JwtAuthenticationOptions.cs`
- Modify: `src/Security/Extensions/AuthenticationServiceCollectionExtensions.cs` (add `AddTokenAuthentication`)
- Rename + rewrite: `tests/Security/JwtMiddlewareTests.cs` → `tests/Security/AuthenticationMiddlewareTests.cs`
- Test: `tests/Security/AuthenticationServiceCollectionExtensionsTests.cs` (extend existing file with registration-validation tests)
- Modify: `docs/content/configuration.md` and `docs/content/security.md` references to `JwtMiddleware` (final wording handled in Task 8; here just keep examples compiling/consistent if they appear in test/sample code — production doc prose is Task 8)

**Interfaces:**
- Consumes: `TokenExtractor` (1), `ITokenValidator` (2), `AuthenticationOptions` (3), `JwtTokenValidator` (4), `IAuthenticationProvider.GetAuthenticatedUserByEmail` (5), `IGoogleTokenVerifier`/`GoogleTokenVerifier`/`GoogleTokenValidator` (6).
- Produces: `class AuthenticationMiddleware(RequestDelegate next, SettingsProvider settings, AuthenticationOptions options, IEnumerable<ITokenValidator> validators) : WebApiMiddleware`; and `IServiceCollection AddTokenAuthentication(this IServiceCollection services, Action<AuthenticationOptions> configure)`.

- [ ] **Step 1: Write the failing middleware tests**

Create `tests/Security/AuthenticationMiddlewareTests.cs`. Rebuild the coverage from the old `JwtMiddlewareTests` around the new constructor, plus the both-schemes cases. Construct the middleware with a real `JwtTokenValidator` and, where Google is exercised, a `GoogleTokenValidator` backed by a fake verifier.

```csharp
using System.Text;
using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Middleware;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthenticationMiddlewareTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private sealed class StubProvider(AuthenticatedUser? byId = null, AuthenticatedUser? byEmail = null) : IAuthenticationProvider
    {
        public AuthenticatedUser? GetAuthenticatedUserById(int id) => byId;
        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }

    private sealed class FakeVerifier(GoogleTokenPayload? payload) : IGoogleTokenVerifier
    {
        public Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences) => Task.FromResult(payload);
    }

    private static SettingsProvider EmptySettings() => new(new ConfigurationBuilder().Build());

    private static JwtConfiguration Config() => new(3600, "issuer", "audience", Secret, new Dictionary<string, string>());

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    private static AuthenticationMiddleware Middleware(
        RequestDelegate next, AuthenticationOptions options, IEnumerable<ITokenValidator> validators) =>
        new(next, EmptySettings(), options, validators);

    private static ITokenValidator Jwt(AuthenticationOptions options) =>
        new JwtTokenValidator(Config(), new JwtHandler(), options);

    private static (DefaultHttpContext Context, StringBuilder Log) BuildContext(
        string? headerToken, IAuthenticationProvider? provider, string? cookieName = null, string? cookieValue = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (headerToken is not null)
        {
            context.Request.Headers.Authorization = $"Bearer {headerToken}";
        }

        if (cookieName is not null && cookieValue is not null)
        {
            context.Request.Headers.Cookie = $"{cookieName}={cookieValue}";
        }

        context.RequestServices = new ServiceCollection()
            .AddSingleton(provider ?? new StubProvider())
            .BuildServiceProvider();

        return (context, new StringBuilder());
    }

    private static async Task<string> ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Jwt_ClaimsOnly_SetsUserAndCallsNext()
    {
        var options = new AuthenticationOptions { JwtMode = JwtValidationMode.ClaimsOnly };
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var (context, log) = BuildContext(token, provider: null);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options)]);

        await middleware.InvokeAsync(context);

        var user = Assert.IsType<AuthenticatedUser>(context.Items["User"]);
        Assert.Equal(42, user.Id);
        Assert.Equal("next", log.ToString());
    }

    [Fact]
    public async Task NoValidTokenReturns401()
    {
        var options = new AuthenticationOptions();
        var (context, log) = BuildContext("not-a-token", provider: null);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options)]);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Empty(log.ToString());
    }

    [Fact]
    public async Task CookieSource_ReadsTokenFromCookie()
    {
        var options = new AuthenticationOptions { Source = TokenSource.Cookie, CookieName = "access_token" };
        var token = CreateToken(new AuthenticatedUser(1, 1).ToTokenClaims());
        var (context, log) = BuildContext(headerToken: null, provider: null, cookieName: "access_token", cookieValue: token);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options)]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        Assert.NotNull(context.Items["User"]);
    }

    [Fact]
    public async Task BothEnabled_AcceptsGoogleToken_WhenNotAJwt()
    {
        var options = new AuthenticationOptions { EnableGoogle = true, GoogleClientIds = { "cid" } };
        var provider = new StubProvider(byEmail: new AuthenticatedUser(7, 2));
        var (context, log) = BuildContext("google.id.token", provider);
        var google = new GoogleTokenValidator(new FakeVerifier(new GoogleTokenPayload("u@e.com", "sub")), options);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options), google]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        var user = Assert.IsType<AuthenticatedUser>(context.Items["User"]);
        Assert.Equal(7, user.Id);
    }

    [Fact]
    public async Task BothEnabled_AcceptsAppJwt()
    {
        var options = new AuthenticationOptions { EnableGoogle = true, GoogleClientIds = { "cid" } };
        var token = CreateToken(new AuthenticatedUser(11, 1).ToTokenClaims());
        var (context, log) = BuildContext(token, provider: null);
        var google = new GoogleTokenValidator(new FakeVerifier(payload: null), options);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options), google]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        Assert.Equal(11, Assert.IsType<AuthenticatedUser>(context.Items["User"]).Id);
    }

    [Fact]
    public async Task AllowAnonymousEndpoint_SkipsValidation()
    {
        var options = new AuthenticationOptions();
        var (context, log) = BuildContext(headerToken: null, provider: null);
        var endpoint = new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new Attributes.AllowAnonymousAttribute()), "anon");
        context.SetEndpoint(endpoint);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options)]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
    }
}
```

Note the `using ArturRios.Util.WebApi.Security` namespace for `Attributes.AllowAnonymousAttribute` — add `using ArturRios.Util.WebApi.Security.Attributes;` and use `AllowAnonymousAttribute` directly if simpler. Delete the old `tests/Security/JwtMiddlewareTests.cs` (`git rm`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests`
Expected: FAIL — `AuthenticationMiddleware` does not exist.

- [ ] **Step 3: Create `AuthenticationMiddleware` (rewrite of the old middleware)**

`git mv src/Security/Middleware/JwtMiddleware.cs src/Security/Middleware/AuthenticationMiddleware.cs`, then replace its contents:

```csharp
using ArturRios.Configuration.Providers;
using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace ArturRios.Util.WebApi.Security.Middleware;

/// <summary>
/// Authenticates requests by extracting one token (from the header, a cookie, or either, per
/// <see cref="AuthenticationOptions.Source"/>) and running it through the enabled
/// <see cref="ITokenValidator"/>s in order; the first that resolves a user attaches it to
/// <c>HttpContext.Items["User"]</c>. Swagger and <see cref="AllowAnonymousAttribute"/> endpoints are skipped.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="settings">Configuration used to detect Swagger routes.</param>
/// <param name="options">Controls the token source and which validators run.</param>
/// <param name="validators">The enabled validators, tried in registration order (app JWT first, Google second).</param>
public class AuthenticationMiddleware(
    RequestDelegate next,
    SettingsProvider settings,
    AuthenticationOptions options,
    IEnumerable<ITokenValidator> validators) : WebApiMiddleware
{
    private readonly ITokenValidator[] _validators = validators.ToArray();

    /// <summary>Validates the request token and, on success, attaches the authenticated user before invoking the next middleware; otherwise writes a 401 response.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        var skipRoute =
            IsSwaggerRoute(context.Request.Path.Value ?? string.Empty) ||
            endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null;

        if (skipRoute)
        {
            await next(context);

            return;
        }

        var token = TokenExtractor.Extract(context, options.Source, options.CookieName);

        string? lastError = null;

        foreach (var validator in _validators)
        {
            var (user, error) = await validator.ValidateAsync(token, context);

            if (user is not null)
            {
                context.Items["User"] = user;

                await next(context);

                return;
            }

            lastError = error;
        }

        await WriteUnauthorized(context, lastError);
    }

    private static async Task WriteUnauthorized(HttpContext context, string? authError)
    {
        var output = ProcessOutput.New.WithError(authError ?? "Unauthorized");

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var payload = JsonConvert.SerializeObject(output);

        await context.Response.WriteAsync(payload);
    }

    private bool IsSwaggerRoute(string path) =>
        settings.GetBool(AppSettingsKeys.SwaggerEnabled) is true &&
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Delete `JwtAuthenticationOptions`**

`git rm src/Security/Configuration/JwtAuthenticationOptions.cs`.

- [ ] **Step 5: Add the `AddTokenAuthentication` registration helper**

In `src/Security/Extensions/AuthenticationServiceCollectionExtensions.cs`, add the method (keep the existing `AddCachedAuthenticationProvider`). Add `using`s for `ArturRios.Util.WebApi.Security.Authentication`, `ArturRios.Util.WebApi.Security.Configuration`, `Microsoft.Extensions.DependencyInjection.Extensions`:

```csharp
    /// <summary>
    /// Registers the consolidated <see cref="AuthenticationOptions"/> and the enabled token validators used by
    /// <c>AuthenticationMiddleware</c>. Validators are registered app-JWT first, Google second, so the middleware
    /// tries them in that order. The app must separately register <c>JwtConfiguration</c> and <c>JwtHandler</c>
    /// (for JWT) and an <see cref="IAuthenticationProvider"/> (required for Google and for JWT <c>Revalidate</c> mode).
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Configures the options.</param>
    /// <exception cref="ArgumentException">No scheme enabled, or Google enabled without any client IDs.</exception>
    public static IServiceCollection AddTokenAuthentication(
        this IServiceCollection services, Action<AuthenticationOptions> configure)
    {
        var options = new AuthenticationOptions();
        configure(options);

        if (!options.EnableJwt && !options.EnableGoogle)
        {
            throw new ArgumentException("At least one authentication scheme (JWT or Google) must be enabled.");
        }

        if (options.EnableGoogle && options.GoogleClientIds.Count == 0)
        {
            throw new ArgumentException("EnableGoogle requires at least one entry in GoogleClientIds.");
        }

        services.AddSingleton(options);

        if (options.EnableJwt)
        {
            services.AddSingleton<ITokenValidator, JwtTokenValidator>();
        }

        if (options.EnableGoogle)
        {
            services.TryAddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();
            services.AddSingleton<ITokenValidator, GoogleTokenValidator>();
        }

        return services;
    }
```

- [ ] **Step 6: Write registration-validation tests**

Add to `tests/Security/AuthenticationServiceCollectionExtensionsTests.cs`:

```csharp
[Fact]
public void AddTokenAuthentication_Throws_WhenNoSchemeEnabled()
{
    var services = new ServiceCollection();
    Assert.Throws<ArgumentException>(() =>
        services.AddTokenAuthentication(o => { o.EnableJwt = false; o.EnableGoogle = false; }));
}

[Fact]
public void AddTokenAuthentication_Throws_WhenGoogleEnabledWithoutClientIds()
{
    var services = new ServiceCollection();
    Assert.Throws<ArgumentException>(() =>
        services.AddTokenAuthentication(o => { o.EnableGoogle = true; }));
}

[Fact]
public void AddTokenAuthentication_RegistersJwtValidator_ByDefault()
{
    var services = new ServiceCollection();
    services.AddTokenAuthentication(_ => { });
    var provider = services.BuildServiceProvider();

    var validators = provider.GetServices<ITokenValidator>().ToArray();
    Assert.Single(validators);
    Assert.IsType<JwtTokenValidator>(validators[0]);
}

[Fact]
public void AddTokenAuthentication_RegistersBothValidators_JwtFirst_WhenGoogleEnabled()
{
    var services = new ServiceCollection();
    services.AddTokenAuthentication(o => { o.EnableGoogle = true; o.GoogleClientIds.Add("cid"); });
    var provider = services.BuildServiceProvider();

    var validators = provider.GetServices<ITokenValidator>().ToArray();
    Assert.Equal(2, validators.Length);
    Assert.IsType<JwtTokenValidator>(validators[0]);
    Assert.IsType<GoogleTokenValidator>(validators[1]);
}
```

Add any needed `using`s (`ArturRios.Util.WebApi.Security.Authentication`, `ArturRios.Util.WebApi.Security.Interfaces`, `Microsoft.Extensions.DependencyInjection`). Note: constructing the provider does not instantiate the validators (they need `JwtConfiguration`/`JwtHandler` which aren't registered here), so `GetServices<ITokenValidator>()` returning the registrations without resolving them is what these tests check — if resolution is attempted, register dummy `JwtConfiguration`/`JwtHandler`/`IGoogleTokenVerifier` singletons in the test. Prefer asserting on registration via `services` `ServiceDescriptor`s if resolution proves awkward: `Assert.Equal(2, services.Count(d => d.ServiceType == typeof(ITokenValidator)));` and check `ImplementationType` order.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests`
Expected: PASS — all new middleware and registration tests green; whole suite green.

- [ ] **Step 8: Build the whole solution to confirm no stale `JwtMiddleware`/`JwtAuthenticationOptions` references remain**

Run: `dotnet build src` then `grep -rn "JwtMiddleware\|JwtAuthenticationOptions" src`
Expected: build succeeds; grep returns no matches in `src` (docs are handled in Task 8).

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: replace JwtMiddleware with validator-based AuthenticationMiddleware"
```

---

### Task 8: Documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/content/configuration.md`
- Modify: `docs/content/security.md`

**Interfaces:**
- Consumes: everything above. Docs must reflect `AuthenticationMiddleware`, the cookie/header/either token source, Google auth setup, `AuthenticationOptions`, `AddTokenAuthentication`, and the new provider method.

- [ ] **Step 1: Update the middleware name in examples**

In `docs/content/configuration.md` (and any README pipeline example), replace `typeof(JwtMiddleware)` with `typeof(AuthenticationMiddleware)` and any prose naming `JwtMiddleware` with `AuthenticationMiddleware`.

- [ ] **Step 2: Update `docs/content/security.md`**

Read the file first. Replace the JWT-only authentication description with: (a) the token source options (`Header`/`Cookie`/`Either` + `CookieName`); (b) the two schemes (`EnableJwt`, `EnableGoogle`) and that a request may carry either; (c) Google setup — add `Google.Apis.Auth`, set `GoogleClientIds`, implement `IAuthenticationProvider.GetAuthenticatedUserByEmail`; (d) registration via `AddTokenAuthentication(o => { ... })`; (e) that `IAuthenticationProvider` is required for Google and for JWT `Revalidate`. Keep the existing authorization (`[Authorize]`/`[RoleRequirement]`) content.

- [ ] **Step 3: Update the README security row**

In `README.md`'s feature table, update the Security row to mention header/cookie token sources and Google ID token validation alongside the app JWT.

- [ ] **Step 4: Verify no stale references**

Run: `grep -rn "JwtMiddleware\|JwtAuthenticationOptions" README.md docs/content`
Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add README.md docs/content/configuration.md docs/content/security.md
git commit -m "docs: document token sources and Google authentication"
```

---

## Final verification

- [ ] `dotnet build src` — 0 errors, no new warnings.
- [ ] `dotnet test tests` — green (existing 49 + new tests).
- [ ] `grep -rn "JwtMiddleware\|JwtAuthenticationOptions" src README.md docs/content` — no output.
- [ ] Manual trace: with `EnableJwt` + `EnableGoogle`, a request with an app JWT authenticates via `JwtTokenValidator`; a request with a Google ID token authenticates via `GoogleTokenValidator`; a cookie-delivered token works when `Source` is `Cookie`/`Either`.

## Notes for the implementer

- **Version bump:** intentionally NOT in this plan. Item A deferred its major bump; when this branch is ready, a single `2.0.0` bump covers A + B + C. Do not edit `<Version>` here unless instructed.
- **`Google.Apis.Auth` version** (`1.72.0`) is a starting point — if it does not resolve, take the current stable via `dotnet add src package Google.Apis.Auth` and record the resolved version.
- The `AuthenticationMiddleware` and validators are singletons (convention-based middleware) — never inject `IAuthenticationProvider` into their constructors; resolve it from `context.RequestServices` inside `ValidateAsync`.
