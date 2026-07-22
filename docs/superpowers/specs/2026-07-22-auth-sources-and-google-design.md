# Authentication token sources (header/cookie) + Google authentication

**Date:** 2026-07-22
**Status:** Approved (pending written-spec review)

## Goal

Extend `ArturRios.Util.WebApi` authentication so that:

- **B —** the token can be read from the `Authorization` **header**, a **cookie**, or **either**
  (header first, then cookie), configurable per app.
- **C —** an app can validate a user with a **Google ID token (OIDC JWT)**, delivered by cookie or
  header, mapping the Google identity to the app's own user via `IAuthenticationProvider`.

A single app may accept **either** its own HMAC JWT **or** a Google token on the same request
(both schemes enabled at once).

This is items **B** and **C** of a three-part effort. Item **A** (decouple logging) is designed in
[`2026-07-22-decouple-logging-design.md`](2026-07-22-decouple-logging-design.md).

## Background — current auth (all custom middleware)

- [`JwtMiddleware`](../../../src/Security/Middleware/JwtMiddleware.cs) reads a `Bearer` token from the
  `Authorization` header, validates the signature with `JwtHandler` (HMAC secret, `ArturRios.Jwt`),
  and resolves the user either from claims (`JwtValidationMode.ClaimsOnly`) or via
  `IAuthenticationProvider.GetAuthenticatedUserById` (`Revalidate`).
- There is **no** `AddAuthentication`/`UseAuthentication`. `Microsoft.AspNetCore.Authentication.JwtBearer`
  is used only for the Swagger security-scheme label in `WebApiStartup`.
- Identity is `AuthenticatedUser(int Id, int Role)`.
- Anonymous/Swagger routes are skipped via `AllowAnonymousAttribute` and the Swagger path check.

## Success criteria

- A raw token can be extracted from header, cookie, or either, selected by configuration.
- With Google enabled and a valid Google ID token (matching a configured client ID), the request is
  authenticated and the app user is resolved by email.
- With both schemes enabled, a request bearing an app HMAC JWT **or** a Google ID token authenticates;
  an invalid/absent token yields the existing 401 JSON (`ProcessOutput`).
- Anonymous/Swagger skip behavior is unchanged.
- `dotnet build`: 0 errors. `dotnet test`: green, with new tests covering the cases in **Testing**.

## Non-goals (YAGNI)

- Google **OAuth access tokens** or a server-side Google **redirect/callback** flow. ID-token
  validation only.
- Refresh-token issuance/rotation, cookie **writing** (login sets the cookie; that is the app's job),
  or CSRF handling for cookie auth beyond documentation notes.
- Concrete `IAuthenticationProvider` implementations (still the consumer's responsibility).
- Multiple simultaneous tokens on one request. Exactly one token is extracted per request.
- Changing `AuthenticatedUser`'s shape.

## Architecture

### Validator strategy behind one middleware

Rename `JwtMiddleware` → **`AuthenticationMiddleware`**. It:

1. Skips anonymous/Swagger routes (unchanged logic).
2. Extracts **one** raw token via `TokenExtractor` using the configured `TokenSource`.
3. Runs the token through the enabled `ITokenValidator`s **in order**; the first that yields a user
   wins. The user is attached to `HttpContext.Items["User"]` and the pipeline continues.
4. If no validator yields a user → 401 with the existing `ProcessOutput` JSON.

```
AuthenticationMiddleware
  ├─ TokenExtractor.Extract(context, source, cookieName) -> string token
  ├─ foreach validator in enabledValidators (JWT first, Google second):
  │     (user, error) = await validator.ValidateAsync(token, context)
  │     if user != null -> attach & next()
  └─ else -> 401
```

### `ITokenValidator`

```csharp
public interface ITokenValidator
{
    Task<TokenValidationResult> ValidateAsync(string token, HttpContext context);
}

// result: AuthenticatedUser? User, string? Error
public readonly record struct TokenValidationResult(AuthenticatedUser? User, string? Error);
```

Two implementations:

- **`JwtTokenValidator`** — today's HMAC path. Validates the signature via `JwtHandler`; on success
  resolves the user by `JwtValidationMode` (claims via `AuthenticatedUserFactory.FromToken`, or
  `IAuthenticationProvider.GetAuthenticatedUserById`). Local/cheap → **tried first**.
- **`GoogleTokenValidator`** — validates the Google ID token, then resolves the app user by email.
  Network-backed (JWKS, cached in-process by the Google library) → **tried second**.

Trying both is safe: an app JWT fails Google's audience/issuer checks and a Google token fails the
HMAC signature check, so a wrong-type token simply falls through to the next validator.

### B — token source extraction

- `enum TokenSource { Header, Cookie, Either }`.
- `TokenExtractor` (static or injectable) reads:
  - **Header:** the `Authorization: Bearer <token>` value (existing `ExtractBearerToken` logic moves here).
  - **Cookie:** `context.Request.Cookies[cookieName]`.
  - **Either:** header first; if empty, cookie.
- Returns `string.Empty` when nothing is found (validators then fail as today).
- Token source is orthogonal to validator choice — both validators receive the extracted token.

### C — Google validation + identity mapping

- Add NuGet **`Google.Apis.Auth`**.
- `GoogleTokenValidator` calls `GoogleJsonWebSignature.ValidateAsync(token, settings)` with
  `Audience = GoogleClientIds`. The library verifies signature (Google JWKS), issuer
  (`accounts.google.com` / `https://accounts.google.com`), audience, and expiry.
- To keep it unit-testable without network access, the actual Google call sits behind a small seam,
  e.g. `IGoogleTokenVerifier` with a default implementation wrapping `GoogleJsonWebSignature`. Tests
  substitute a fake verifier.
- On success, read the verified `Email` claim and call the new provider method; a hit yields the
  `AuthenticatedUser`, a miss yields `"User not found"`.

### `IAuthenticationProvider` change (breaking)

Add:

```csharp
AuthenticatedUser? GetAuthenticatedUserByEmail(string email);
```

`CachedAuthenticationProvider` implements it with the same memory-cache/negative-cache behavior it
already applies to id lookups (cache key namespaced by lookup kind). Existing implementers must add
the method — acceptable under the major version bump.

### Options / registration model

One consolidated options object (absorbs `JwtAuthenticationOptions`):

```csharp
public class AuthenticationOptions
{
    public TokenSource Source { get; set; } = TokenSource.Header;
    public string CookieName { get; set; } = "access_token";
    public bool EnableJwt { get; set; } = true;
    public bool EnableGoogle { get; set; } = false;
    public JwtValidationMode JwtMode { get; set; } = JwtValidationMode.ClaimsOnly;
    public IList<string> GoogleClientIds { get; set; } = new List<string>();
}
```

Validation at registration:

- `EnableGoogle == true` requires `GoogleClientIds` non-empty **and** an `IAuthenticationProvider`
  registered (Google always maps by email through the provider).
- `EnableJwt == false && EnableGoogle == false` is a configuration error (nothing would authenticate).
- `JwtMode == Revalidate` (or Google enabled) requires an `IAuthenticationProvider`.

`JwtValidationMode` is retained (referenced by `AuthenticationOptions.JwtMode`).
`JwtAuthenticationOptions` is removed (folded in).

## Breaking changes (ride the planned major bump from item A)

- `JwtMiddleware` → `AuthenticationMiddleware` (rename).
- `JwtAuthenticationOptions` removed; replaced by `AuthenticationOptions`.
- `IAuthenticationProvider` gains `GetAuthenticatedUserByEmail`.

## Testing (TDD)

- **`TokenExtractor`:** header present/absent/malformed; cookie present/absent; `Either` falls back
  header→cookie; empty when nothing found.
- **`JwtTokenValidator`:** valid token (ClaimsOnly), valid token (Revalidate → provider hit/miss),
  invalid signature, unreadable token.
- **`GoogleTokenValidator`:** valid token → email lookup hit; valid token → email miss (`User not
  found`); verifier rejects (audience/issuer/expiry) → error. Uses a fake `IGoogleTokenVerifier`.
- **`CachedAuthenticationProvider`:** email lookup caches positive and negative results independently
  of id lookups.
- **`AuthenticationMiddleware`:** anonymous/Swagger skip; JWT-only enabled; Google-only enabled;
  both enabled with an app JWT; both enabled with a Google token; no valid token → 401.
- **Registration validation:** Google enabled without client IDs / without provider throws; no scheme
  enabled throws.

## Open items for implementation

- Final class/namespace placement of `ITokenValidator`, `TokenValidationResult`, `TokenExtractor`,
  `IGoogleTokenVerifier` (under `Security/...` following existing folder conventions).
- Confirm the `Google.Apis.Auth` package version at implementation time.
- Documentation updates (README + `docs/content`) to cover the new options, cookie source, and Google
  setup — done as part of implementation.
