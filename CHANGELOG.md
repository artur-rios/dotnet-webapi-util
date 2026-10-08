# Changelog

All notable changes to `ArturRios.Util.WebApi` are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `AuthenticationOptions.ValidateIssuer` and `ValidateAudience` (both default `false`). When on,
  `JwtTokenValidator` also requires the app JWT's `iss` to equal `JwtConfiguration.Issuer` and its `aud` to include
  `JwtConfiguration.Audience` (ordinal), failing with `"Invalid token issuer"`/`"Invalid token audience"`. Turning one
  on while the matching `JwtConfiguration` value is blank makes the application fail at startup with an
  `InvalidOperationException`. With both off, only the signature is checked, as before.

### Fixed

- `WebApiSwaggerOptions.JwtAuthentication` now makes the generated document require the `Bearer` scheme. The
  security requirement was written as an empty object (`"security": [{}]`), which tells Swagger UI the operations need
  no credentials, so the token entered under "Authorize" was never sent.
- A malformed bearer token (bad base64url, a segment that is not JSON) is answered with 401 when Google
  authentication is enabled. `GoogleTokenVerifier` let the decoder's `FormatException`/`JsonException` escape, so
  `ExceptionMiddleware` answered 500.
- `ExceptionMiddleware` answers a `BadHttpRequestException` (e.g. a request body over `MaxRequestBodySize`) with the
  exception's own status code (413, 400, …) and its reason phrase, logged at Information, instead of a 500 logged as
  an error.

### Security

- Without the new `ValidateIssuer`/`ValidateAudience`, any token signed with an accepted key is accepted whatever
  issuer or audience it names. Turn them on when the signing key is shared with another issuer or audience (see the
  Security docs, "Issuer and audience").

## [5.1.0] - 2026-10-07

### Added

- `TraceActivityOptions` with `LogClientIp` and `TagClientAddress`, exposed on `WebApiStartupOptions` as
  `TraceActivity`, to stop `TraceActivityMiddleware` logging the client IP or tagging `client.address`. Both default
  to `true`, so existing behavior is unchanged.

## [5.0.0] - 2026-10-05

### Added

- `output.ToActionResult(...)` extensions for `DataOutput<T>`, `PaginatedOutput<T>` and `ProcessOutput`.
- Every `WebApiStartup` step is a public extension usable on a plain `WebApplicationBuilder`: `LoadConfiguration`,
  `AddWebApiSwagger`, `AddInvalidModelStateEnvelope`, `UseStandardMiddlewares`, `UseWebApiMiddlewares` and
  `UseWebApiSwagger`.
- `WebApiStartupOptions` (Swagger, invalid-model-state envelope, CORS policy, extra middlewares) and
  `WebApiSwaggerOptions` (allowed environments, JWT authentication, generator callback).
- `WebApiParameters.ToWebApplicationOptions()` and `IsSwaggerAllowed(...)`.
- `AddTracePropagation()` on `IHttpClientBuilder` to add `TracePropagationHandler` to a typed client.
- `TraceActivityMiddleware` continues an incoming `traceparent`/`tracestate` when there is no ambient activity, logs
  the client IP with each request and tags it on the activity as `client.address`. `TracePropagationHandler` also
  forwards `tracestate`.
- `[AllowAnonymous]` can be placed on a whole controller, and ASP.NET Core's own `[AllowAnonymous]` is honored too.

### Changed

- **Breaking:** `WebApiStartup` is a fixed template with one abstract method, `ConfigureServices`; `Build()` returns
  the `WebApplication`.
- **Breaking:** error envelopes written by the middlewares are camelCase, like MVC results. `[Authorize]` returns the
  standard failed envelope, and `[RoleRequirement]` returns 401 when no user is authenticated.
- **Breaking:** `UseWebApiMiddlewares` throws for types that do not derive from `WebApiMiddleware` instead of skipping
  them.
- `WebApiParameters` keys are case-insensitive, and `UseAppSettings` is accepted alongside `UseAppSetting`.
- `AuthenticateAndAuthorizeAsync` throws `WebApiClientException` when the response is not valid or carries no token.
- Extensions are written as C# 14 extension blocks.

### Removed

- **Breaking:** `ResponseResolver` (use `output.ToActionResult()`) and `TokenExtractor` (use `context.ExtractToken()`).
- **Breaking:** the empty `WebApiStartup` hooks, `ConfigureApp`, `BuildApp`, `BuildAndRun`, the `App` field and the
  startup helper methods, replaced by the extensions above.

## [4.0.0] - 2026-08-24

### Changed

- **Breaking:** `ExceptionMiddleware` envelopes its 500 response with errors rather than messages, so it no longer
  comes back as `"success": true`.
- The middlewares serialize with `System.Text.Json` rather than an undeclared, transitively resolved
  `Newtonsoft.Json`.
- `ArturRios.Configuration`, `ArturRios.Jwt` and `ArturRios.Util` updated to 1.2.0, 1.2.0 and 2.1.0;
  `Google.Apis.Auth` updated to 1.76.0.

### Removed

- **Breaking:** the `Microsoft.AspNetCore.Mvc.Testing` dependency. Nothing in the package used it; a consumer that
  relied on getting it transitively must now reference it directly.

### Fixed

- `[EndpointToggle]` no longer keeps per-request state on the shared attribute instance, where concurrent requests to
  the same action overwrote each other's context.
- The exception form of `[EndpointToggle]` throws with the configured disabled message rather than the default one.

## [3.3.0] - 2026-08-19

### Changed

- `ArturRios.Configuration` updated from 1.0.0 to 1.1.0 and `ArturRios.Util` from 1.5.0 to 2.0.0. The public API is
  unchanged.

## [3.2.0] - 2026-08-17

### Added

- Signing key rotation: tokens are validated against `JwtConfiguration.Keys` when it has any, so a token signed with a
  key that is no longer the signing key stays valid until that key is withdrawn. A configuration with no `Keys` still
  validates against `Secret`.

### Changed

- `ArturRios.Jwt` updated to 1.1.0.

## [3.1.0] - 2026-08-12

### Changed

- Dependencies updated: `ArturRios.Util` 1.5.0, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 and
  `Microsoft.OpenApi` 2.7.5.

## [3.0.0] - 2026-07-30

### Added

- `IAuthenticatedUser` (`Guid Id`, `int RoleId`), so a consuming app can authenticate users of its own type, read back
  with `HttpContext.GetUser<TUser>()`.
- `IAuthenticatedUserMapper`, with `DefaultAuthenticatedUserMapper`, to decide which claims a token carries and how a
  user is rebuilt from them.
- `TokenClaimsReader` to read a JWT's claims.

### Changed

- **Breaking:** users are identified by a `Guid` id and an `int` role id: `AuthenticatedUser(Guid Id, int RoleId)`,
  `TokenClaimKeys.RoleId`, `GetAuthenticatedUserById(Guid)`, and `IAuthenticationProvider` and `TokenValidationResult`
  speak `IAuthenticatedUser`. Tokens issued by 2.x carry an integer `id` claim and are rejected after the upgrade. See
  [Upgrading from 2.x to 3.0](#upgrading-from-2x-to-30).

### Removed

- **Breaking:** `AuthenticatedUserFactory` and `ToTokenClaims()`, replaced by `TokenClaimsReader` and the mapper.

### Fixed

- `TokenClaimsReader.Read` returns `null` instead of throwing for a token whose header cannot be decoded.

### Upgrading from 2.x to 3.0

`3.0.0` makes the authenticated-user type and the token's claim keys caller-defined. Where the library
used to hard-code `AuthenticatedUser(int Id, int Role)`, it now only knows `IAuthenticatedUser` (`Guid
Id`, `int RoleId`) — implement your own, or keep using the library's `AuthenticatedUser(Guid Id, int
RoleId)`.

| Before | After |
|---|---|
| `AuthenticatedUser(int Id, int Role)` | `AuthenticatedUser(Guid Id, int RoleId)`, or your own `IAuthenticatedUser` |
| `user.Role` | `user.RoleId` |
| `TokenClaimKeys.Role` | `TokenClaimKeys.RoleId` — the claim string is still `"role"` |
| `GetAuthenticatedUserById(int id)` returning `AuthenticatedUser?` | `GetAuthenticatedUserById(Guid id)` returning `IAuthenticatedUser?` |
| `GetAuthenticatedUserByEmail(string email)` returning `AuthenticatedUser?` | same parameter, now returning `IAuthenticatedUser?` |
| `TokenValidationResult(AuthenticatedUser?, string?)` | `TokenValidationResult(IAuthenticatedUser?, string?)` — affects custom `ITokenValidator` implementations |
| `user.ToTokenClaims()` | `mapper.ToClaims(user)` |
| `AuthenticatedUserFactory.FromToken(token)` | `TokenClaimsReader.Read(token)` then `mapper.FromClaims(claims)` |
| `(AuthenticatedUser?)HttpContext.Items["User"]` | `HttpContext.GetUser<MyUser>()` |

Two things to plan around before you deploy this upgrade:

**Tokens issued by 2.x are rejected after the upgrade.** The claim key strings are unchanged, so the
claims still *read* — but `DefaultAuthenticatedUserMapper` requires the `id` claim to parse as a `Guid`,
and 2.x wrote an integer there. Every unexpired access token therefore fails with a 401
(`"Could not retrieve user from token"`, or `"Could not retrieve user id from token"` in `Revalidate`
mode). This is fail-closed and safe, but it logs every user out at the moment of deployment. Plan for
it: drain the old tokens before switching over, accept the forced re-authentication, or ship a mapper
that accepts both an integer and a `Guid` in the `id` claim for one release.

**User ids must become `Guid`s.** If your store keys users by integer, this is a data migration, not
just a compile fix.

## [2.1.0] - 2026-07-23

### Added

- `ResponseResolver.Resolve` takes a status-code map to override the default status per output.

### Changed

- Dependencies updated, including `ArturRios.Util` 1.3.0 and `Google.Apis.Auth` 1.75.0.

## [2.0.0] - 2026-07-22

### Added

- Token sources: `AuthenticationMiddleware` reads the token from the `Authorization` header, a cookie, or either.
- Google ID token authentication alongside the app's own JWT, configured with `AddTokenAuthentication` and
  `AuthenticationOptions`.
- `IAuthenticationProvider.GetAuthenticatedUserByEmail`, with caching in `CachedAuthenticationProvider`.
- `ITokenValidator` and `TokenValidationResult` contracts for custom validators.

### Changed

- **Breaking:** `JwtMiddleware` is replaced by `AuthenticationMiddleware`.

### Removed

- **Breaking:** `JwtAuthenticationOptions`, replaced by `AuthenticationOptions`.
- **Breaking:** `AddLogging`/`AddCustomLogging` and the `ArturRios.Logging` dependency; set up logging in the derived
  `WebApiStartup`.

### Security

- Google ID tokens whose email is missing or unverified are rejected before the user lookup.

## [1.1.0] - 2026-07-14

### Added

- `[EndpointToggle]` to enable or disable a single endpoint from a compile-time flag or a configuration value.

## [1.0.0] - 2026-07-09

### Added

- `WebApiStartup` and `WebApiParameters` to bootstrap a web API (configuration, Swagger, middleware pipeline).
- JWT authentication middleware with stateless (`ClaimsOnly`) or revalidating user resolution,
  `CachedAuthenticationProvider`, and `[Authorize]`, `[AllowAnonymous]` and `[RoleRequirement]`.
- `ExceptionMiddleware`, `TraceActivityMiddleware` and `TracePropagationHandler`.
- `BaseWebApiClient` and `BaseWebApiClientRoute` for typed HTTP clients, and `WebApiClientException`.
- `ResponseResolver` to turn `ArturRios.Output` envelopes into `ActionResult`s.
- `CredentialsValidator` for email format and password length.
- XML documentation across the public API. The configuration and client types live in the
  `ArturRios.Util.WebApi.Configuration` and `ArturRios.Util.WebApi.Client` namespaces.

[Unreleased]: https://github.com/artur-rios/dotnet-webapi-util/compare/5.1.0...HEAD
[5.1.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/5.0.0...5.1.0
[5.0.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/4.0.0...5.0.0
[4.0.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v3.3.0...4.0.0
[3.3.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v3.2.0...v3.3.0
[3.2.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v3.1.0...v3.2.0
[3.1.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v2.1.0...v3.0.0
[2.1.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v1.1.0...v2.0.0
[1.1.0]: https://github.com/artur-rios/dotnet-webapi-util/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/artur-rios/dotnet-webapi-util/releases/tag/v1.0.0
