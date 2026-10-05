---
title: Configuration
weight: 20
description: >-
  `WebApiStartup` is the abstract base class you derive from to bootstrap an ASP.NET Core host: configuration loading, Swagger, the middleware pipeline and the...
---

`WebApiStartup` is the abstract base class you derive from to bootstrap an ASP.NET Core host:
configuration loading, Swagger, the middleware pipeline and the invalid-model-state response all
go through a small set of methods on it, some of which you must implement and some of which are optional
hooks.

## The lifecycle

```mermaid
flowchart TB
    New["new Startup(args)<br/><i>builds WebApplicationBuilder + WebApiParameters</i>"] --> Build["Build() — abstract, you implement it"]
    Build --> LoadConfig["LoadConfiguration()"]
    Build --> InvalidModel["AddCustomInvalidModelStateResponse()"]
    Build --> SwaggerGen["UseSwaggerGen(...)"]
    Build --> Deps["AddDependencies() — virtual hook"]
    Build --> BuildApp["BuildApp() — builds App from Builder"]
    Build --> ConfigureApp["ConfigureApp() — abstract, you implement it"]
    ConfigureApp --> Middlewares["AddMiddlewares([...])"]
    ConfigureApp --> Swagger["UseSwagger(...)"]
    ConfigureApp --> Map["App.MapControllers() / endpoints"]
    New --> Run["Run() or BuildAndRun()"]
```

`Build()` and `ConfigureApp()` are **abstract** — you must implement both. Everything else on
`WebApiStartup` is either a plain helper method you call from inside them, or a **virtual no-op hook**
you can override:

| Member | Kind | Purpose |
|---|---|---|
| `Build()` | abstract | The full startup sequence: configuration, services, `BuildApp()`, then `ConfigureApp()`. |
| `ConfigureApp()` | abstract | Configures the built `App`'s request pipeline (middlewares, Swagger UI, endpoint mapping). |
| `AddDependencies()` | virtual hook | Override to register your own services. No-op by default. |
| `ConfigureCors()` | virtual hook | Override to enable and configure CORS. No-op by default. |
| `ConfigureSecurity()` | virtual hook | Override to configure authentication/authorization services. No-op by default. |
| `ConfigureWebApi()` | virtual hook | Override to configure web-API-specific services (controllers, filters, etc). No-op by default. |
| `StartServices()` | virtual hook | Override to start background/hosted services. No-op by default. |
| `LoadConfiguration()` | helper | Loads `appsettings`/env file per `WebApiParameters`; always registers `SettingsProvider` (needed by `AuthenticationMiddleware`), and `EnvironmentProvider` when the env file is loaded. |
| `AddMiddlewares(params Type[])` | helper | Registers each given middleware type on `App`, in order, via `App.UseWebApiMiddlewares(...)`; throws an `ArgumentException` for any type that isn't a `WebApiMiddleware`. |
| `AddCustomInvalidModelStateResponse()` | helper | Replaces ASP.NET Core's default invalid-model-state response with a `DataOutput<string>`-shaped 400. |
| `UseSwaggerGen(...)` | helper | Registers the Swagger generator, conditionally on the current environment. |
| `UseSwagger(...)` | helper | Enables the Swagger middleware (JSON + UI), conditionally on the current environment. |
| `BuildApp()` | helper | Builds `App` from `Builder`. Must run before `ConfigureApp()` touches `App`. |
| `Run()` / `BuildAndRun()` | helper | Runs the built app, or calls `Build()` then `Run()` in one call. |

## A minimal `Startup`

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

`BuildApp()` must run before `ConfigureApp()`, since the pipeline configuration in `ConfigureApp()`
(`AddMiddlewares`, `UseSwagger`, endpoint mapping) operates on the built `App`, not the `Builder`.

Outside `WebApiStartup`, the same registration is available as an `IApplicationBuilder` extension in
`ArturRios.Util.WebApi.Extensions`, with the same ordering and the same `ArgumentException` for a type
that isn't a `WebApiMiddleware`:

```csharp
app.UseWebApiMiddlewares(
    typeof(TraceActivityMiddleware),
    typeof(ExceptionMiddleware),
    typeof(AuthenticationMiddleware));
```

## `WebApiParameters` — command-line startup args

`WebApiParameters` parses the process's `args` into a small set of properties, without any code changes
required at the call site. Each argument is a `Key:Value` pair with a case-insensitive key; unrecognized
or malformed entries are silently ignored, leaving the default in place:

| Argument | Property | Default | Effect |
|---|---|---|---|
| `Environment:<name>` | `EnvironmentName` | `""` | Sets the environment name, if it's a valid `EnvironmentType` value, and applies it as the host environment (overriding `ASPNETCORE_ENVIRONMENT`). |
| `UseAppSettings:<bool>` | `UseAppSettings` | `true` | Whether `LoadConfiguration()` loads `appsettings.json`. The legacy spelling `UseAppSetting` is still accepted. |
| `UseEnvFile:<bool>` | `UseEnvFile` | `true` | Whether `LoadConfiguration()` loads a `.env` file. |
| `EnableSwaggerDocs:<bool>` | `EnableSwaggerDocs` | `true` | `false` disables Swagger (generation and UI) in every environment, whatever else is configured. |
| `SwaggerEnvironments:[A,B]` | `SwaggerEnvironments` | `[]` | The environment names in which Swagger is served (see below). |

For example:

```
Environment:Production UseAppSettings:false UseEnvFile:false SwaggerEnvironments:[Development,Staging]
```

## Swagger — enabled per environment

Swagger is gated by **environment name**, not by a simple on/off flag. `UseSwagger(allowedEnvironments)`
and `UseSwaggerGen(allowedEnvironments, ...)` each decide whether to activate using this precedence:

1. The `EnableSwaggerDocs:false` CLI arg, if supplied, is a kill switch: Swagger is off in every
   environment and nothing below is consulted.
2. The `allowedEnvironments` parameter passed directly to the call, if non-empty.
3. Otherwise, `WebApiParameters.GetSwaggerEnvironments()` — the `SwaggerEnvironments:[...]` CLI arg, if it
   parsed to at least one valid environment name.
4. Otherwise, the built-in default: `Development` and `Local`.

```csharp
UseSwaggerGen(jwtAuthentication: true); // uses the default/CLI-configured environments
UseSwagger([EnvironmentType.Development, EnvironmentType.Staging]); // explicit override
```

Because `GetSwaggerEnvironments()` always falls back to `[Development, Local]` rather than returning an
empty list, in practice Swagger's on/off state is always decided by the kill switch and the environment
rules above — there is no separate `appsettings.json` toggle you need to flip to turn Swagger on or off.

`AppSettingsKeys.SwaggerEnabled` (`"Swagger:Enabled"`) is a separate internal marker. `UseSwagger` sets it
whenever it actually serves Swagger, whichever rule allowed it (`LoadConfiguration()` also sets it early
when an explicit `SwaggerEnvironments:[...]` argument includes the current environment).
`AuthenticationMiddleware` reads this marker to recognize Swagger routes — `/swagger` and anything under
`/swagger/`, matched by path segment — and skip authentication on them. So whenever Swagger is served, its
routes bypass authentication; when it isn't, they don't.

## Logging

`WebApiStartup` no longer configures logging — that is the derived class's responsibility. Because the
library's logging types depend only on `Microsoft.Extensions.Logging.ILogger<T>`, any backend works:
the default ASP.NET Core providers (already registered by `WebApplication.CreateBuilder`), Serilog
(`Builder.Host.UseSerilog(...)`), or a custom `ILogger`. Configure whichever you prefer inside your
`Build()` override before calling `BuildApp()`.

## Invalid model state

`AddCustomInvalidModelStateResponse()` replaces ASP.NET Core's built-in `[ApiController]` validation
response with a `DataOutput<string>`-shaped 400: each invalid model-binding parameter is turned into an
error message of the form `Parameter: <name> | Error: <message>`, collected on the envelope's `Errors`
list, so validation failures come back in the same shape as any other failed `ToActionResult`
call.

## Where to next

- **[Architecture](../architecture/)** — how the pipeline, security model and response envelopes fit
  together.
- **[Security](../security/)** — `ConfigureSecurity()`, JWT validation modes, and role-based authorization.
- **[Middleware & Diagnostics](../middleware-and-diagnostics/)** — the built-in middlewares registered via
  `AddMiddlewares`.
- **[Responses](../responses/)** — `ToActionResult` and the invalid-model-state envelope shape.
- **[Endpoint Toggling](../endpoint-toggle/)** — reading `appsettings.json`/environment values to enable or
  disable individual endpoints.
