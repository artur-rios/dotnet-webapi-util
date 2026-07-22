# ArturRios.Util.WebApi

[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-webapi-util)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)
[![NuGet](https://img.shields.io/nuget/v/ArturRios.Util.WebApi.svg)](https://www.nuget.org/packages/ArturRios.Util.WebApi)

Utilities for building ASP.NET Core web APIs in .NET: a base class for bootstrapping the host
(configuration, Swagger, middleware pipeline), token authentication (app JWT and/or Google ID tokens, read
from the header, a cookie, or either) with stateless-or-revalidating user resolution and role-based
authorization, cross-cutting middleware for exceptions and distributed tracing, a thin typed-`HttpClient`
base for calling other services, and a resolver that turns `ArturRios.Output` envelopes into
`ActionResult`s.

## Install

```bash
dotnet add package ArturRios.Util.WebApi
```

Requires **.NET 10**.

## Feature overview

| Area | What it does | Docs |
|---|---|---|
| Configuration / bootstrap | `WebApiStartup` wires up configuration loading, Swagger and the middleware pipeline behind a small set of virtual hooks; `WebApiParameters` parses command-line startup args. | [Configuration](https://artur-rios.github.io/dotnet-webapi-util/configuration/) |
| Security (JWT + Google + roles) | `AuthenticationMiddleware` reads a token from the header, a cookie, or either, validates it as the app's own JWT and/or a Google ID token, and attaches an `AuthenticatedUser`, in stateless (`ClaimsOnly`) or per-request-revalidated mode; `[Authorize]`, `[AllowAnonymous]` and `[RoleRequirement(...)]` declare access rules. | [Security](https://artur-rios.github.io/dotnet-webapi-util/security/) |
| Middleware & diagnostics | `ExceptionMiddleware` converts unhandled exceptions into a JSON error envelope; `TraceActivityMiddleware` and `TracePropagationHandler` propagate a W3C `traceparent` across a request and its outgoing calls. | [Middleware & diagnostics](https://artur-rios.github.io/dotnet-webapi-util/middleware-and-diagnostics/) |
| HTTP client | `BaseWebApiClient` / `BaseWebApiClientRoute` give a typed client a shared `HttpGateway`, route grouping, and helpers to authenticate and carry the resulting bearer token on subsequent calls. | [HTTP client](https://artur-rios.github.io/dotnet-webapi-util/http-client/) |
| Responses | `ResponseResolver.Resolve(...)` wraps `DataOutput<T>`, `PaginatedOutput<T>` and `ProcessOutput` in an `ActionResult`, defaulting to 200/400 based on `Success` unless a status code is supplied. | [Responses](https://artur-rios.github.io/dotnet-webapi-util/responses/) |
| Endpoint toggling | `[EndpointToggle]` enables or disables a single endpoint from a compile-time flag or a runtime `appsettings.json`/environment-variable value, shaping the disabled response as an empty status code, the action's default value, a `ProcessOutput` envelope, or a thrown `EndpointDisabledException`. | [Endpoint toggling](https://artur-rios.github.io/dotnet-webapi-util/endpoint-toggle/) |

See also **[Architecture](https://artur-rios.github.io/dotnet-webapi-util/architecture/)** for how these pieces fit together.

## Quick start

### Configuration / bootstrap

Derive from `WebApiStartup` and implement `Build()`/`ConfigureApp()` using its hooks:

```csharp
public class Startup(string[] args) : WebApiStartup(args)
{
    public override void Build()
    {
        LoadConfiguration();
        AddCustomInvalidModelStateResponse();
        UseSwaggerGen(jwtAuthentication: true);
        Builder.Services.AddControllers();

        AddDependencies();

        BuildApp();
        ConfigureApp();
    }

    public override void ConfigureApp()
    {
        AddMiddlewares([
            typeof(TraceActivityMiddleware),
            typeof(ExceptionMiddleware),
            typeof(AuthenticationMiddleware)
        ]);

        UseSwagger();
        App.MapControllers();
    }
}

new Startup(args).BuildAndRun();
```

Startup behavior can be tweaked without code changes via command-line args parsed by `WebApiParameters`
(`Environment:Production`, `UseAppSetting:false`, `UseEnvFile:false`,
`SwaggerEnvironments:[Development,Staging]`). Swagger is enabled per environment: it is served in
`Development` and `Local` by default, and the allowed environments can be overridden with the
`SwaggerEnvironments:[...]` arg or by passing `allowedEnvironments` to `UseSwagger` / `UseSwaggerGen`.

### Security

`AuthenticationMiddleware` extracts a token from the request — the `Authorization: Bearer` header, a
cookie, or either, per `AuthenticationOptions.Source` — and runs it through the enabled validators
(the app's own JWT and/or a Google ID token) until one resolves an `AuthenticatedUser`, which is then
attached to `HttpContext.Items["User"]`. Swagger routes and endpoints marked with `[AllowAnonymous]` are
skipped.

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
});
```

At least one of `EnableJwt`/`EnableGoogle` must be enabled, and `EnableGoogle` requires at least one
entry in `GoogleClientIds` — `AddTokenAuthentication` throws otherwise. A request may carry either kind of
token: validators run in registration order (app JWT first, then Google), and the first one that resolves
a user wins.

For the app JWT, how the user is resolved is controlled by `AuthenticationOptions.JwtMode`:

- **`ClaimsOnly` (default)** — the user is rebuilt from the token's `id` and `role` claims. No data
  store is queried, so authentication costs nothing beyond the signature check. Because nothing is
  re-checked server-side, role changes and revocations only take effect once the token expires — keep
  access-token lifetimes short and use refresh tokens.
- **`Revalidate`** — `IAuthenticationProvider.GetAuthenticatedUserById` is called on every request
  (resolved per-request from the request scope). Guarantees freshness and lets deleted users be
  rejected immediately, at the cost of one lookup per request.

A Google ID token is always resolved by looking up the token's verified email through
`IAuthenticationProvider.GetAuthenticatedUserByEmail`, so an `IAuthenticationProvider` is **required**
whenever `EnableGoogle` is `true`, as it is for JWT `Revalidate` mode. To accept Google sign-in:

1. Add the `Google.Apis.Auth` package (already a dependency of this library, so it resolves
   transitively — add it explicitly only if you call its APIs directly).
2. Set `EnableGoogle = true` and `GoogleClientIds` to your app's OAuth client ID(s) on the web api options.
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
requires the user's role to be one of the given values (403 otherwise); `[AllowAnonymous]` exempts a
single action from both:

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

Register the built-in middlewares (each derives from `WebApiMiddleware`) in pipeline order with
`AddMiddlewares`:

```csharp
AddMiddlewares([
    typeof(TraceActivityMiddleware), // assigns/propagates a W3C trace id
    typeof(ExceptionMiddleware),     // turns unhandled exceptions into a JSON error envelope
    typeof(AuthenticationMiddleware)
]);
```

`TraceActivityMiddleware` puts the current trace id on `HttpContext.TraceIdentifier` and
`HttpContext.Items["TraceId"]` and echoes it on the response's `traceparent` header. To keep that trace
id flowing into calls made with `HttpClient`, register `TracePropagationHandler` as a message handler:

```csharp
builder.Services.AddTransient<TracePropagationHandler>();
builder.Services.AddHttpClient<MyApiClient>()
    .AddHttpMessageHandler<TracePropagationHandler>();
```

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
`Authorization: Bearer` header for every subsequent call made through the shared `Gateway`.

### Responses

`ResponseResolver.Resolve(...)` maps an `ArturRios.Output` envelope to an `ActionResult`, defaulting the
HTTP status to 200 on success and 400 on failure:

```csharp
[HttpGet("{id:int}")]
public ActionResult<DataOutput<UserDto?>> GetById(int id)
{
    DataOutput<UserDto?> output = _userService.GetById(id);

    return ResponseResolver.Resolve(output);
}
```

Overloads also accept `PaginatedOutput<T>` and `ProcessOutput`, and all of them take an optional
explicit `statusCode` to override the default.

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
`disabledMessage` (`Object`, the default), or a thrown `EndpointDisabledException` (`Exception`) that the
exception pipeline handles. The status code defaults to `404 Not Found` and can be overridden with
`disabledStatusCode`.

## Documentation

Full documentation, including architecture diagrams: **<https://artur-rios.github.io/dotnet-webapi-util>**

## Versioning

Semantic Versioning (SemVer). Breaking changes result in a new major version. New methods or non-breaking behavior
changes increment the minor version; fixes or tweaks increment the patch.

Version 2.0 renamed the `ArturRios.Util.WebApi.Api.Configuration` and `ArturRios.Util.WebApi.Api.Client`
namespaces to `ArturRios.Util.WebApi.Configuration` and `ArturRios.Util.WebApi.Client`.

## Build, test and publish

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and publish the project
and Git for source control.
If you want, optional helper toolsets I built to facilitate these tasks are available:

- [Dotnet Tools](https://github.com/artur-rios/dotnet-tools)
- [Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools)

## Legal Details

This project is licensed under the [MIT License](https://en.wikipedia.org/wiki/MIT_License). A copy of the license is
available at [LICENSE](./LICENSE) in the repository.
