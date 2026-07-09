# ArturRios.Util.WebApi

Utilities for developing web API's in .NET

## Authentication

`JwtMiddleware` validates the bearer JSON Web Token on each request, then attaches an
`AuthenticatedUser` to `HttpContext.Items["User"]`. Swagger routes and endpoints marked with
`[AllowAnonymous]` are skipped.

How the user is resolved is controlled by `JwtAuthenticationOptions.ValidationMode`:

- **`ClaimsOnly` (default)** — the user is rebuilt from the token's `id` and `role` claims. No data
  store is queried, so authentication costs nothing beyond the signature check. Because nothing is
  re-checked server-side, role changes and revocations only take effect once the token expires — keep
  access-token lifetimes short and use refresh tokens.
- **`Revalidate`** — `IAuthenticationProvider.GetAuthenticatedUserById` is called on every request
  (resolved per-request from the request scope). Guarantees freshness and lets deleted users be
  rejected immediately, at the cost of one lookup per request.

```csharp
// Opt into per-request revalidation
builder.Services.AddSingleton(new JwtAuthenticationOptions
{
    ValidationMode = JwtValidationMode.Revalidate
});
```

### Caching provider lookups

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

## Versioning

Semantic Versioning (SemVer). Breaking changes result in a new major version. New methods or non-breaking behavior
changes increment the minor version; fixes or tweaks increment the patch.

## Build, test and publish

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and publish the project
and Git for source control.
If you want, optional helper toolsets I built to facilitate these tasks are available:

- [Dotnet Tools](https://github.com/artur-rios/dotnet-tools)
- [Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools)

## Legal Details

This project is licensed under the [MIT License](https://en.wikipedia.org/wiki/MIT_License). A copy of the license is
available at [LICENSE](./LICENSE) in the repository.
