# ArturRios.Util.WebApi

[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-webapi-util)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-webapi-util/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/ArturRios.Util.WebApi.svg)](https://www.nuget.org/packages/ArturRios.Util.WebApi)

Utilities for building ASP.NET Core web APIs in .NET: a base class for bootstrapping the host
(configuration, Swagger, middleware pipeline), token authentication (app JWT and/or Google ID tokens, read
from the header, a cookie, or either) with stateless-or-revalidating user resolution and role-based
authorization, cross-cutting middleware for exceptions and distributed tracing, a thin typed-`HttpClient`
base for calling other services, and `ToActionResult` extensions that turn `ArturRios.Output` envelopes into
`ActionResult`s.

## Installation
```bash
dotnet add package ArturRios.Util.WebApi
```

Requires **.NET 10**.

## Feature overview

| Area | What it does | Docs |
|---|---|---|
| Configuration / bootstrap | `WebApiStartup` runs one standard sequence — configuration loading, Swagger, the middleware pipeline — leaving you a single `ConfigureServices` method, and every step is also a standalone extension; `WebApiParameters` parses command-line startup args. | [Configuration](https://artur-rios.github.io/dotnet-webapi-util/docs/configuration/) |
| Security (JWT + Google + roles) | `AuthenticationMiddleware` reads a token from the header, a cookie, or either, validates it as the app's own JWT and/or a Google ID token, and attaches an `IAuthenticatedUser`, in stateless (`ClaimsOnly`) or per-request-revalidated mode; `[Authorize]`, `[AllowAnonymous]` and `[RoleRequirement(...)]` declare access rules. | [Security](https://artur-rios.github.io/dotnet-webapi-util/docs/security/) |
| Middleware & diagnostics | `ExceptionMiddleware` converts unhandled exceptions into a JSON error envelope; `TraceActivityMiddleware` and `TracePropagationHandler` propagate the W3C `traceparent`/`tracestate` across a request and its outgoing calls. | [Middleware & diagnostics](https://artur-rios.github.io/dotnet-webapi-util/docs/middleware-and-diagnostics/) |
| HTTP client | `BaseWebApiClient` / `BaseWebApiClientRoute` give a typed client a shared `HttpGateway`, route grouping, and helpers to authenticate and carry the resulting bearer token on subsequent calls. | [HTTP client](https://artur-rios.github.io/dotnet-webapi-util/docs/http-client/) |
| Responses | `output.ToActionResult(...)` wraps `DataOutput<T>`, `PaginatedOutput<T>` and `ProcessOutput` in an `ActionResult`, defaulting to 200/400 based on `Success` unless a status code is supplied. | [Responses](https://artur-rios.github.io/dotnet-webapi-util/docs/responses/) |
| Endpoint toggling | `[EndpointToggle]` enables or disables a single endpoint from a compile-time flag or a runtime `appsettings.json`/environment-variable value, shaping the disabled response as an empty status code, the action's default value, a `ProcessOutput` envelope, or a thrown `EndpointDisabledException`. | [Endpoint toggling](https://artur-rios.github.io/dotnet-webapi-util/docs/endpoint-toggle/) |

See also **[Architecture](https://artur-rios.github.io/dotnet-webapi-util/docs/architecture/)** for how these pieces fit together.

## Quick start

### Configuration / bootstrap

Derive from `WebApiStartup`, tune the standard sequence through its options, and register your own
services in `ConfigureServices` — the one method you implement:

```csharp
public class Startup(string[] args) : WebApiStartup(args, options =>
{
    options.Swagger.JwtAuthentication = true;
    options.Middlewares.Add(typeof(MyMiddleware));
})
{
    protected override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddTokenAuthentication(_ => { });
        builder.Services.AddHealthChecks();
        builder.Services.AddScoped<IMyService, MyService>();
    }
}

var app = new Startup(args).Build();
app.MapHealthChecks("/health");
app.Run();
```

`Build()` always runs the same sequence: load configuration, add controllers, the invalid-model-state
envelope (`options.UseInvalidModelStateEnvelope`, on by default) and Swagger, call `ConfigureServices`,
build the app, then add `TraceActivityMiddleware`, `ExceptionMiddleware` and — once
`AddTokenAuthentication` has been called — `AuthenticationMiddleware`, serve Swagger, add the extra
`options.Middlewares`, and map the controllers. It returns the `WebApplication`, so further endpoints (health
checks, minimal APIs) are mapped on it; `Run()`/`RunAsync()` build and run in one call when there are none.

Every step is also a public extension in `ArturRios.Util.WebApi.Configuration`, so the same setup works on a
plain `WebApplicationBuilder`:

```csharp
var parameters = new WebApiParameters(args);
var builder = WebApplication.CreateBuilder(parameters.ToWebApplicationOptions());

builder.LoadConfiguration(parameters);
builder.Services.AddControllers();
builder.Services.AddInvalidModelStateEnvelope();
builder.AddWebApiSwagger(parameters, swagger => swagger.JwtAuthentication = true);
builder.Services.AddTokenAuthentication(_ => { });

var app = builder.Build();

app.UseStandardMiddlewares(); // trace, exceptions, Swagger, CORS (when a policy is named), authentication
app.UseWebApiMiddlewares(typeof(MyMiddleware));
app.MapControllers();
app.Run();
```

Startup behavior can be tweaked without code changes via command-line args parsed by `WebApiParameters`
(`Environment:Production`, `UseAppSettings:false`, `UseEnvFile:false`, `EnableSwaggerDocs:false`,
`SwaggerEnvironments:[Development,Staging]`; keys are case-insensitive). `Environment:<name>` becomes the
host environment, overriding `ASPNETCORE_ENVIRONMENT`. Swagger is enabled per environment: it is served in
`Development` and `Local` by default, and the allowed environments can be overridden with the
`SwaggerEnvironments:[...]` arg or with `options.Swagger.AllowedEnvironments`;
`EnableSwaggerDocs:false` turns it off in every environment.

### Security

`AuthenticationMiddleware` extracts a token from the request — the `Authorization: Bearer` header, a
cookie, or either, per `AuthenticationOptions.Source` — and runs it through the enabled validators
(the app's own JWT and/or a Google ID token) until one resolves an `IAuthenticatedUser`, which is then
attached to `HttpContext.Items["User"]`. Swagger routes (whenever Swagger is served) and endpoints marked
with `[AllowAnonymous]` are skipped; any other request without a resolved user gets a 401 with a failed
`ProcessOutput` envelope (`"Authentication token not provided"` when no token was found).

Register it with `AddTokenAuthentication`:

```csharp
builder.Services.AddTokenAuthentication(options =>
{
    options.Source = TokenSource.Either;  // Header | Cookie | Either (default: Header)
    options.CookieName = "access_token";  // default
    options.EnableJwt = true;             // default
    options.EnableGoogle = true;          // default: false
    options.GoogleClientIds = ["your-google-oauth-client-id"];
    options.JwtMode = JwtValidationMode.ClaimsOnly; // or Revalidate
    options.ValidateIssuer = true;        // default: false
    options.ValidateAudience = true;      // default: false
});
```

At least one of `EnableJwt`/`EnableGoogle` must be enabled, and `EnableGoogle` requires at least one
entry in `GoogleClientIds` — `AddTokenAuthentication` throws otherwise. By default only the app JWT's
signature is checked; `ValidateIssuer`/`ValidateAudience` also require its `iss`/`aud` to match
`JwtConfiguration.Issuer`/`Audience` — turn them on whenever the signing key is shared with another issuer or
audience. Turning one on while the matching `JwtConfiguration` value is blank fails the application's startup. A request may carry either kind of
token: validators run in registration order (app JWT first, then Google), and the first one that resolves
a user wins.

For the app JWT, how the user is resolved is controlled by `AuthenticationOptions.JwtMode`:

- **`ClaimsOnly` (default)** — the registered mapper rebuilds the user from the token's claims. No data
  store is queried, so authentication costs nothing beyond the signature check. Because nothing is
  re-checked server-side, role changes and revocations only take effect once the token expires — keep
  access-token lifetimes short and use refresh tokens.
- **`Revalidate`** — `IAuthenticationProvider.GetAuthenticatedUserById(Guid)` is called on every request
  (resolved per-request from the request scope). Guarantees freshness and lets deleted users be
  rejected immediately, at the cost of one lookup per request.
- **Your own identity** — implement `IAuthenticatedUser` (`Guid Id`, `int RoleId`) on your own type and an
  `IAuthenticatedUserMapper` to decide what your tokens carry; read it back with
  `HttpContext.GetUser<MyUser>()`. See [Security](https://artur-rios.github.io/dotnet-webapi-util/docs/security/).

Signing keys can be rotated: give `JwtConfiguration` a set of `Keys` and name the one that signs with
`SigningKeyId`, and a token signed with a key that has since been retired stays valid until that key is
withdrawn. A configuration with no `Keys` validates against `Secret` exactly as before. See
[Security](https://artur-rios.github.io/dotnet-webapi-util/docs/security/).

A Google ID token is always resolved by looking up the token's verified email through
`IAuthenticationProvider.GetAuthenticatedUserByEmail`, so an `IAuthenticationProvider` is **required**
whenever `EnableGoogle` is `true`, as it is for JWT `Revalidate` mode. To accept Google sign-in:

1. Add the `Google.Apis.Auth` package (already a dependency of this library, so it resolves
   transitively — add it explicitly only if you call its APIs directly).
2. Set `EnableGoogle = true` and `GoogleClientIds` to your app's OAuth client ID(s) in the `AddTokenAuthentication`
   callback (`AuthenticationOptions`).
3. Implement `IAuthenticationProvider.GetAuthenticatedUserByEmail(string)` (and register the provider,
   optionally via `AddCachedAuthenticationProvider<T>`, below).

#### Caching provider lookups

When using `Revalidate`, wrap your `IAuthenticationProvider` with `CachedAuthenticationProvider` to
serve repeated lookups of the same user from an `IMemoryCache` within a short time-to-live:

```csharp
builder.Services.AddCachedAuthenticationProvider<MyAuthenticationProvider>(options =>
{
    options.Ttl = TimeSpan.FromSeconds(30); // default: 60s
    options.CacheMisses = true;             // also cache "user not found" (default: false)
});
```

This bounds staleness to the TTL while collapsing bursts of requests from the same user into a single
store hit.

#### Declaring access rules

`[Authorize]` requires an authenticated user (401 otherwise); `[RoleRequirement(...)]` additionally
requires the user's role to be one of the given values (403 otherwise); `[AllowAnonymous]` — this
library's or ASP.NET Core's own — exempts an action or a whole controller from both. Both filters answer
with a failed `ProcessOutput` envelope (`errors: ["Unauthorized"]` for a 401):

```csharp
[Authorize]
[RoleRequirement(1, 2)] // e.g. Admin, Manager
public class AccountsController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login(Credentials credentials) { /* ... */ }

    [HttpGet]
    public IActionResult GetAll() { /* only roles 1 and 2 reach here */ }
}
```

### Middleware & diagnostics

The built-in middlewares (each derives from `WebApiMiddleware`) are added in pipeline order by
`UseStandardMiddlewares()`, which `WebApiStartup` calls for you:

1. `TraceActivityMiddleware` — assigns/propagates a W3C trace id;
2. `ExceptionMiddleware` — turns unhandled exceptions into a JSON error envelope;
3. `AuthenticationMiddleware` — only when `AddTokenAuthentication` was registered.

Your own `WebApiMiddleware` types run after them, in order — listed in `options.Middlewares`, or passed to
`app.UseWebApiMiddlewares(...)` outside `WebApiStartup`; a type that doesn't derive from `WebApiMiddleware`
throws an `ArgumentException`:

```csharp
public class Startup(string[] args) : WebApiStartup(args, options =>
{
    options.Middlewares.Add(typeof(RequestTimingMiddleware));
    options.Middlewares.Add(typeof(TenantMiddleware));
})
{
    // ...
}
```

`TraceActivityMiddleware` puts the current trace id on `HttpContext.TraceIdentifier` and
`HttpContext.Items["TraceId"]` and echoes it on the response's `traceparent` header, continuing an
incoming `traceparent`/`tracestate` when there is no ambient activity. To keep that trace id flowing into
calls made with `HttpClient`, add `TracePropagationHandler` to the client with `AddTracePropagation()`:

```csharp
builder.Services.AddHttpClient<MyApiClient>()
    .AddTracePropagation();
```

`TraceActivityMiddleware` also logs the client's IP address with each request and tags it on the activity as
`client.address`.
Both are on by default and can be turned off, for example where IP addresses count as personal data:

```csharp
public class Startup(string[] args) : WebApiStartup(args, options =>
{
    options.TraceActivity.LogClientIp = false;      // log only the trace id
    options.TraceActivity.TagClientAddress = false; // don't tag client.address
})
{
    // ...
}
```

Outside `WebApiStartup`, configure `TraceActivityOptions` like any options type
(`builder.Services.Configure<TraceActivityOptions>(...)`).

### HTTP client

Derive `BaseWebApiClient` for the client and `BaseWebApiClientRoute` for each group of related routes:

```csharp
public class MyApiClient : BaseWebApiClient
{
    public AccountsRoute Accounts { get; private set; } = null!;

    public MyApiClient(HttpClient httpClient) : base(httpClient) { }

    protected override void SetRoutes()
    {
        Accounts = new AccountsRoute(Gateway);
    }
}

public class AccountsRoute(HttpGateway gateway) : BaseWebApiClientRoute(gateway)
{
    public override string BaseUrl => "/accounts";

    public Task LoginAsync(Credentials credentials) =>
        AuthenticateAndAuthorizeAsync(credentials, $"{BaseUrl}/login");
}
```

`AuthenticateAndAuthorizeAsync` posts the credentials, then applies the returned token as the
`Authorization: Bearer` header for every subsequent call made through the shared `Gateway`; it throws a
`WebApiClientException` when the response isn't valid or carries no token.

### Responses

The `ToActionResult(...)` extension (`ArturRios.Util.WebApi.AspNetCore`) maps an `ArturRios.Output`
envelope to an `ActionResult`, defaulting the HTTP status to 200 on success and 400 on failure:

```csharp
[HttpGet("{id:int}")]
public ActionResult<DataOutput<UserDto?>> GetById(int id)
{
    DataOutput<UserDto?> output = _userService.GetById(id);

    return output.ToActionResult();
}
```

Overloads also accept `PaginatedOutput<T>` and `ProcessOutput`, and all of them take an optional
explicit `statusCode` and `statusMap` to override the default. They replace `ResponseResolver.Resolve(...)`,
which was removed in 5.0.0.

### Endpoint toggling

`[EndpointToggle]` turns a single endpoint on or off. The compile-time form fixes the state in code; the
configuration form re-reads it on every request from `appsettings.json` and/or environment variables, so
an endpoint can be disabled without a redeploy:

```csharp
public class ReportsController : ControllerBase
{
    // Off in code — always returns the disabled response.
    [EndpointToggle(isEnabled: false)]
    [HttpGet("legacy")]
    public IActionResult Legacy() { /* ... */ }

    // Read from configuration key "Endpoints:Reports:Export" on every request.
    [EndpointToggle(ConfigurationSourceType.AppSettings)]
    [HttpGet("export")]
    public IActionResult Export() { /* ... */ }
}
```

When the endpoint is disabled, `disabledOutputType` decides the shape of the response — an empty status
code (`Void`), the action's default return value (`Default`), a `ProcessOutput` envelope carrying
`disabledMessage` as an error (`Object`, the default), or a thrown `EndpointDisabledException`
(`Exception`) that `ExceptionMiddleware` answers with the same status code. The status code defaults to `404 Not Found` and can be overridden with
`disabledStatusCode`.

## Documentation

Full documentation, including architecture diagrams: **<https://artur-rios.github.io/dotnet-webapi-util>**

## Upgrading

- From 2.x to 3.0: [Upgrading from 2.x to 3.0](https://github.com/artur-rios/dotnet-webapi-util/blob/main/CHANGELOG.md#upgrading-from-2x-to-30)

## Changelog

Notable changes in each release are recorded in [CHANGELOG.md](https://github.com/artur-rios/dotnet-webapi-util/blob/main/CHANGELOG.md). Releases follow
[Semantic Versioning](https://semver.org/).

## Contributing

Building from source, running the tests, the branching model and the release process are described in
[CONTRIBUTING.md](https://github.com/artur-rios/dotnet-webapi-util/blob/main/CONTRIBUTING.md).

## Legal Details

This project is licensed under the [MIT License](https://en.wikipedia.org/wiki/MIT_License). A copy of the license is
available at [LICENSE](https://github.com/artur-rios/dotnet-webapi-util/blob/main/LICENSE) in the repository.
