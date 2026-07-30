# Caller-Defined Identity and Claims Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a consuming app define its own authenticated-user type and token claims, while the library knows only a `Guid Id` and an `int RoleId`.

**Architecture:** Identity becomes an interface (`IAuthenticatedUser`) that the app's own type implements, and claim mapping moves into a caller-registered `IAuthenticatedUserMapper` owning both directions (user → claims at login, claims → user at validation). The library keeps one JWT-reading helper (`TokenClaimsReader`) and stops knowing any claim keys outside the default mapper.

**Tech Stack:** .NET 10, C# (latest lang version, nullable + implicit usings enabled), xUnit, `ArturRios.Jwt` (`JwtHandler`/`JwtConfiguration`), `System.IdentityModel.Tokens.Jwt`, ASP.NET Core middleware/filters.

**Spec:** [`2026-07-30-caller-defined-identity-design.md`](../specs/2026-07-30-caller-defined-identity-design.md)

## Global Constraints

- The library knows exactly two user properties: `Guid Id` and `int RoleId`. Nothing else about the user may leak into library code.
- `ArturRios.Jwt` is an upstream package and must not be modified. `JwtHandler.GetUserIdFromToken` returns `int?` and is therefore unusable — never call it.
- `TokenClaimKeys` string values stay `"id"` and `"role"` so already-issued tokens still read. Only the C# constant name changes (`Role` → `RoleId`).
- `[Authorize]`, `[AllowAnonymous]`, and `[RoleRequirement(params int[] authorizedRoles)]` keep their current signatures and their current 401/403 response bodies.
- `GenerateDocumentationFile` is `true` in [`src/ArturRios.Util.WebApi.csproj`](../../../src/ArturRios.Util.WebApi.csproj): **every public type and member needs an XML doc comment**, or the build emits CS1591 warnings. The bar for every task is `dotnet build`: 0 errors **and 0 warnings**.
- `ImplicitUsings` is enabled: `System`, `System.Collections.Generic`, `System.Linq` need no `using`. `System.IdentityModel.Tokens.Jwt` and `System.Security.Claims` do.
- Test names follow the `Method_Condition_Result` style already used in `tests/Security` (not Given-When-Then — match the surrounding files).
- Build: `dotnet build src/ArturRios.Util.WebApi.csproj`. Test: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`. There is no solution file; always name the project.
- Every task ends with a green build and green tests. No task may leave the tree uncompilable.

## Starting state — read this before Task 1

The branch is `feat/caller-defined-identity`. Its only commit is the design doc. The working tree carries **uncommitted** changes from a superseded refactor that widened `AuthenticatedUser.Id` from `int` to `string` (7 source files, 7 test files, plus `docs/content/security.md`), including a `AuthenticatedUserFactory.IdFromToken` method added to replace the unusable `JwtHandler.GetUserIdFromToken`.

That work is **superseded, not wrong** — Task 2 replaces `string` with `Guid` in the same places. Task 1 Step 0 commits it as-is so nothing is lost and the diff stays reviewable.

If the human running this plan would rather drop that detour entirely, they can run `git restore .` before starting and skip Step 0; Task 2's instructions produce the same end state either way. **Do not run `git restore` on your own initiative** — it discards uncommitted work irreversibly.

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `src/Security/Interfaces/IAuthenticatedUser.cs` | The two properties the library knows (create) | 1 |
| `src/Security/Constants/AuthenticationItemKeys.cs` | The `HttpContext.Items` key, internal (create) | 1 |
| `src/Security/Extensions/HttpContextExtensions.cs` | `GetUser()` / `GetUser<TUser>()` (create) | 1 |
| `src/Security/Records/AuthenticatedUser.cs` | Default `IAuthenticatedUser` implementation | 2 |
| `src/Security/Constants/TokenClaimKeys.cs` | Default mapper's claim keys | 2 |
| `src/Security/Interfaces/IAuthenticationProvider.cs` | Lookup by `Guid` / email | 2 |
| `src/Security/Providers/CachedAuthenticationProvider.cs` | Caching decorator | 2 |
| `src/Security/Records/TokenValidationResult.cs` | Validator outcome | 2 |
| `src/Security/Filters/RoleRequirementFilter.cs` | Reads `RoleId` | 2 |
| `src/Security/Attributes/AuthorizeAttribute.cs` | Presence check | 2 |
| `src/Security/Middleware/AuthenticationMiddleware.cs` | Attaches the user | 2 |
| `src/Security/Authentication/TokenClaimsReader.cs` | JWT → claims dictionary; the only `JwtSecurityTokenHandler` user (create) | 3 |
| `src/Security/Interfaces/IAuthenticatedUserMapper.cs` | Both mapping directions (create) | 4 |
| `src/Security/Mappers/DefaultAuthenticatedUserMapper.cs` | Zero-config mapper and reference implementation (create) | 4 |
| `src/Security/Authentication/JwtTokenValidator.cs` | Uses reader + mapper | 5 |
| `src/Security/Extensions/AuthenticationServiceCollectionExtensions.cs` | `AddTokenAuthentication<TMapper>` | 5 |
| `src/Security/Factories/AuthenticatedUserFactory.cs` | **Deleted** (folder becomes empty and is removed) | 5 |
| `src/Security/Extensions/AuthenticationExtensions.cs` | **Deleted** — replaced by `ToClaims` | 5 |
| `src/ArturRios.Util.WebApi.csproj` | Version → `3.0.0` | 6 |
| `docs/content/security.md`, `docs/content/architecture.md`, `README.md` | Documentation | 6 |

Test files mirror this: `tests/Security/HttpContextExtensionsTests.cs` (Task 1), `AuthorizeAttributeTests.cs` (Task 2), `TokenClaimsReaderTests.cs` (Task 3), `DefaultAuthenticatedUserMapperTests.cs` (Task 4), and updates to the seven existing `tests/Security` files across Tasks 2 and 5.

---

### Task 1: Identity interface, item key, and HttpContext accessors

Purely additive — nothing consumes these yet, so the build and the existing suite stay green throughout.

**Files:**
- Create: `src/Security/Interfaces/IAuthenticatedUser.cs`
- Create: `src/Security/Constants/AuthenticationItemKeys.cs`
- Create: `src/Security/Extensions/HttpContextExtensions.cs`
- Test: `tests/Security/HttpContextExtensionsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `IAuthenticatedUser` with `Guid Id { get; }` and `int RoleId { get; }`; `internal static class AuthenticationItemKeys` with `public const string User = "User"`; `HttpContextExtensions.GetUser(this HttpContext)` → `IAuthenticatedUser?` and `HttpContextExtensions.GetUser<TUser>(this HttpContext)` → `TUser?` where `TUser : class, IAuthenticatedUser`.

- [ ] **Step 0: Commit the superseded working-tree change (skip if the human already ran `git restore .`)**

```bash
git add -A
git commit -m "refactor: widen authenticated user id to string

Superseded by the Guid id in the caller-defined identity design; kept
as a checkpoint so the diff is not lost."
```

- [ ] **Step 1: Write the failing test**

Create `tests/Security/HttpContextExtensionsTests.cs`. The test double implements the new interface directly, so this test is meaningful before `AuthenticatedUser` implements it:

```csharp
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Security;

public class HttpContextExtensionsTests
{
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private sealed record TestUser(Guid Id, int RoleId) : IAuthenticatedUser;

    private sealed record OtherUser(Guid Id, int RoleId) : IAuthenticatedUser;

    private static HttpContext ContextWithUser(IAuthenticatedUser? user)
    {
        var context = new DefaultHttpContext();

        if (user is not null)
        {
            context.Items["User"] = user;
        }

        return context;
    }

    [Fact]
    public void GetUser_ReturnsInterface_WhenUserAttached()
    {
        var context = ContextWithUser(new TestUser(UserId, 3));

        var user = context.GetUser();

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void GetUser_ReturnsNull_WhenNoUserAttached()
    {
        Assert.Null(ContextWithUser(null).GetUser());
    }

    [Fact]
    public void GetUserOfT_ReturnsConcreteType_WhenTypesMatch()
    {
        var context = ContextWithUser(new TestUser(UserId, 3));

        var user = context.GetUser<TestUser>();

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
    }

    [Fact]
    public void GetUserOfT_ReturnsNull_WhenTypeDoesNotMatch()
    {
        var context = ContextWithUser(new OtherUser(UserId, 3));

        Assert.Null(context.GetUser<TestUser>());
    }

    [Fact]
    public void GetUserOfT_ReturnsNull_WhenNoUserAttached()
    {
        Assert.Null(ContextWithUser(null).GetUser<TestUser>());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --filter HttpContextExtensionsTests`
Expected: build failure — `CS0246: The type or namespace name 'IAuthenticatedUser' could not be found` and `CS1061` for `GetUser`.

- [ ] **Step 3: Create the interface**

`src/Security/Interfaces/IAuthenticatedUser.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>The user identity the library needs to authenticate a request and enforce role-based access.
/// Consuming apps implement this on their own type, adding whatever else they need.</summary>
public interface IAuthenticatedUser
{
    /// <summary>The user's id, used to look the user up when re-validating against the data store, and as
    /// the provider cache key.</summary>
    Guid Id { get; }

    /// <summary>The user's role id, compared against the roles allowed by
    /// <see cref="Attributes.RoleRequirementAttribute"/>.</summary>
    int RoleId { get; }
}
```

- [ ] **Step 4: Create the item-key constant**

`src/Security/Constants/AuthenticationItemKeys.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Constants;

/// <summary>The <c>HttpContext.Items</c> keys used by the authentication pipeline.</summary>
internal static class AuthenticationItemKeys
{
    /// <summary>The key under which the authenticated user is attached to the current request.</summary>
    public const string User = "User";
}
```

- [ ] **Step 5: Create the HttpContext accessors**

`src/Security/Extensions/HttpContextExtensions.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Extension methods for reading the user attached to the current request by
/// <see cref="Middleware.AuthenticationMiddleware"/>.</summary>
public static class HttpContextExtensions
{
    /// <summary>Gets the authenticated user attached to the request.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The authenticated user, or <see langword="null"/> if the request is not authenticated.</returns>
    public static IAuthenticatedUser? GetUser(this HttpContext context) =>
        context.Items[AuthenticationItemKeys.User] as IAuthenticatedUser;

    /// <summary>Gets the authenticated user as the app's own identity type.</summary>
    /// <typeparam name="TUser">The app's identity type.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The authenticated user, or <see langword="null"/> if the request is not authenticated or the
    /// attached user is of another type.</returns>
    public static TUser? GetUser<TUser>(this HttpContext context) where TUser : class, IAuthenticatedUser =>
        context.Items[AuthenticationItemKeys.User] as TUser;
}
```

The `class` constraint is required for the `as` cast; the spec's shorthand omitted it.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --filter HttpContextExtensionsTests`
Expected: PASS, 5 tests.

- [ ] **Step 7: Verify the whole suite and a warning-free build**

Run: `dotnet build src/ArturRios.Util.WebApi.csproj` then `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`
Expected: `0 Warning(s), 0 Error(s)`; all tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/Security/Interfaces/IAuthenticatedUser.cs src/Security/Constants/AuthenticationItemKeys.cs src/Security/Extensions/HttpContextExtensions.cs tests/Security/HttpContextExtensionsTests.cs
git commit -m "feat: add IAuthenticatedUser and HttpContext accessors"
```

---

### Task 2: Flip identity to Guid Id and int RoleId

This is the atomic type migration. C# will not compile between the first and last edit, so all of it lands in one commit. Nothing here introduces the mapper yet — `AuthenticatedUserFactory` and `ToTokenClaims` survive this task in `Guid` form and are deleted in Task 5.

**Files:**
- Modify: `src/Security/Records/AuthenticatedUser.cs`
- Modify: `src/Security/Constants/TokenClaimKeys.cs`
- Modify: `src/Security/Interfaces/IAuthenticationProvider.cs`
- Modify: `src/Security/Providers/CachedAuthenticationProvider.cs`
- Modify: `src/Security/Records/TokenValidationResult.cs`
- Modify: `src/Security/Extensions/AuthenticationExtensions.cs`
- Modify: `src/Security/Factories/AuthenticatedUserFactory.cs`
- Modify: `src/Security/Authentication/JwtTokenValidator.cs`
- Modify: `src/Security/Filters/RoleRequirementFilter.cs`
- Modify: `src/Security/Attributes/AuthorizeAttribute.cs`
- Modify: `src/Security/Middleware/AuthenticationMiddleware.cs`
- Test: modify `tests/Security/JwtTokenValidatorTests.cs`, `AuthenticationMiddlewareTests.cs`, `CachedAuthenticationProviderTests.cs`, `AuthenticationServiceCollectionExtensionsTests.cs`, `GoogleTokenValidatorTests.cs`, `RoleRequirementFilterTests.cs`, `AuthenticatedUserFactoryTests.cs`
- Test: create `tests/Security/AuthorizeAttributeTests.cs`

**Interfaces:**
- Consumes: `IAuthenticatedUser`, `AuthenticationItemKeys` (Task 1).
- Produces: `record AuthenticatedUser(Guid Id, int RoleId) : IAuthenticatedUser`; `TokenClaimKeys.Id`/`TokenClaimKeys.RoleId`; `IAuthenticationProvider.GetAuthenticatedUserById(Guid id)` → `IAuthenticatedUser?` and `GetAuthenticatedUserByEmail(string email)` → `IAuthenticatedUser?`; `TokenValidationResult(IAuthenticatedUser? User, string? Error)`; `AuthenticatedUserFactory.FromToken(string)` → `AuthenticatedUser?` and `IdFromToken(string)` → `Guid?` (both temporary, deleted in Task 5).

- [ ] **Step 1: Migrate the factory's tests to Guid**

`AuthenticatedUserFactory` still serves the validator until Task 5, so it keeps its coverage until then. Replace `tests/Security/AuthenticatedUserFactoryTests.cs` entirely — the `"not numeric"` cases become `"not a Guid"` cases, and a second `CreateToken` overload lets a test build claims the mapper-shaped helper cannot:

```csharp
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Factories;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthenticatedUserFactoryTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private static string CreateToken(AuthenticatedUser user) => CreateToken(user.ToTokenClaims());

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    [Fact]
    public void FromToken_ShouldReconstructUser_FromIdAndRoleClaims()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3));

        var user = AuthenticatedUserFactory.FromToken(token);

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenIdIsNotAGuid()
    {
        var token = CreateToken(new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, "42" }, { TokenClaimKeys.RoleId, "3" }
        });

        Assert.Null(AuthenticatedUserFactory.FromToken(token));
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenRoleIdIsNotNumeric()
    {
        var token = CreateToken(new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, UserId.ToString() }, { TokenClaimKeys.RoleId, "admin" }
        });

        Assert.Null(AuthenticatedUserFactory.FromToken(token));
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenTokenIsNotReadable()
    {
        Assert.Null(AuthenticatedUserFactory.FromToken("not-a-jwt"));
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenTokenIsEmpty()
    {
        Assert.Null(AuthenticatedUserFactory.FromToken(string.Empty));
    }

    [Fact]
    public void IdFromToken_ShouldReturnId_WhenClaimIsAGuid()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3));

        Assert.Equal(UserId, AuthenticatedUserFactory.IdFromToken(token));
    }

    [Fact]
    public void IdFromToken_ShouldReturnNull_WhenIdIsNotAGuid()
    {
        var token = CreateToken(new Dictionary<string, string> { { TokenClaimKeys.Id, "42" } });

        Assert.Null(AuthenticatedUserFactory.IdFromToken(token));
    }

    [Fact]
    public void IdFromToken_ShouldReturnNull_WhenTokenIsNotReadable()
    {
        Assert.Null(AuthenticatedUserFactory.IdFromToken("not-a-jwt"));
    }
}
```

This file is deleted in Task 5 along with the factory itself; `DefaultAuthenticatedUserMapperTests` (Task 4) carries the same parsing rules forward permanently.

- [ ] **Step 2: Update the tests to the new types (the failing state)**

`tests/Security/RoleRequirementFilterTests.cs` — add a shared id below the existing role constants:

```csharp
    private const int AuthorizedRole = 1;
    private const int UnauthorizedRole = 2;
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");
```

then replace both user constructions:

```csharp
        var user = new AuthenticatedUser(UserId, AuthorizedRole);
```

```csharp
        var user = new AuthenticatedUser(UserId, UnauthorizedRole);
```

Then widen the helper to the interface and add a caller-defined type — the filter must work against a type the library has never seen. Add `using ArturRios.Util.WebApi.Security.Interfaces;`, the record, and the widened signature (`BuildContext`'s body is unchanged):

```csharp
    private sealed record TenantUser(Guid Id, int RoleId, string TenantId) : IAuthenticatedUser;

    private static AuthorizationFilterContext BuildContext(IAuthenticatedUser? user, bool allowAnonymous)
```

and append two tests:

```csharp
    [Fact]
    public void OnAuthorization_CustomUserTypeWithAuthorizedRole_DoesNotSetResult()
    {
        var context = BuildContext(new TenantUser(UserId, AuthorizedRole, "acme"), allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_CustomUserTypeWithUnauthorizedRole_ReturnsForbidden()
    {
        var context = BuildContext(new TenantUser(UserId, UnauthorizedRole, "acme"), allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }
```

Create `tests/Security/AuthorizeAttributeTests.cs` — the attribute has no test file today, and it is the other place a consumer's type meets a cast:

```csharp
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthorizeAttributeTests
{
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private sealed record TenantUser(Guid Id, int RoleId, string TenantId) : IAuthenticatedUser;

    private static AuthorizationFilterContext BuildContext(IAuthenticatedUser? user, bool allowAnonymous)
    {
        var httpContext = new DefaultHttpContext();

        if (user is not null)
        {
            httpContext.Items["User"] = user;
        }

        var actionDescriptor = new ActionDescriptor();

        if (allowAnonymous)
        {
            actionDescriptor.EndpointMetadata = new List<object> { new AllowAnonymousAttribute() };
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void OnAuthorization_CustomUserType_DoesNotSetResult()
    {
        var context = BuildContext(new TenantUser(UserId, 1, "acme"), allowAnonymous: false);

        new AuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_NoUser_ReturnsUnauthorized()
    {
        var context = BuildContext(null, allowAnonymous: false);

        new AuthorizeAttribute().OnAuthorization(context);

        var result = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_AllowAnonymousAndNoUser_DoesNotSetResult()
    {
        var context = BuildContext(null, allowAnonymous: true);

        new AuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }
}
```

Both of these genuinely fail before Step 11: today `AuthorizeAttribute` hard-casts `Items["User"]` to the concrete `AuthenticatedUser` and `RoleRequirementFilter` casts with `as AuthenticatedUser`, so a `TenantUser` is not seen as a user at all — `OnAuthorization_CustomUserType_DoesNotSetResult` and `OnAuthorization_CustomUserTypeWithAuthorizedRole_DoesNotSetResult` fail until the casts move to the interface.

`tests/Security/GoogleTokenValidatorTests.cs` — add the id constant at the top of the class:

```csharp
    private static readonly Guid UserId = Guid.Parse("7b644d0a-9f11-4c5d-8e6a-4b903f2a9c1e");
```

replace the stub's signatures:

```csharp
    private sealed class StubProvider(IAuthenticatedUser? byEmail) : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => null;
        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }
```

and replace both `new AuthenticatedUser("7", 2)` with `new AuthenticatedUser(UserId, 2)` and the assertion `Assert.Equal("7", result.User!.Id)` with `Assert.Equal(UserId, result.User!.Id)`.

`tests/Security/AuthenticationServiceCollectionExtensionsTests.cs` — the fake provider and the lookup:

```csharp
    private sealed class FakeAuthenticationProvider : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => new AuthenticatedUser(id, 1);
        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }
```

```csharp
        var id = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

        var user = Assert.IsType<CachedAuthenticationProvider>(resolved).GetAuthenticatedUserById(id);
        Assert.Equal(id, user!.Id);
```

Add `using ArturRios.Util.WebApi.Security.Interfaces;` if not already present (it is — the file already uses `IAuthenticationProvider`).

`tests/Security/CachedAuthenticationProviderTests.cs` — ids become `Guid`s. Add constants at the top of the class:

```csharp
    private static readonly Guid FirstId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");
    private static readonly Guid SecondId = Guid.Parse("7b644d0a-9f11-4c5d-8e6a-4b903f2a9c1e");
```

both stubs:

```csharp
    private sealed class CountingProvider(IAuthenticatedUser? byId = null, IAuthenticatedUser? byEmail = null) : IAuthenticationProvider
    {
        public int IdCallCount { get; private set; }
        public int EmailCallCount { get; private set; }

        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
        {
            IdCallCount++;
            return byId;
        }

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email)
        {
            EmailCallCount++;
            return byEmail;
        }
    }

    private sealed class CountingAuthenticationProvider(Func<Guid, IAuthenticatedUser?> resolve) : IAuthenticationProvider
    {
        public int CallCount { get; private set; }

        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
        {
            CallCount++;

            return resolve(id);
        }

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }
```

then in the test bodies: every `GetAuthenticatedUserById("42")` becomes `GetAuthenticatedUserById(FirstId)`; `Assert.Equal("42", second!.Id)` becomes `Assert.Equal(FirstId, second!.Id)`; in `DifferentIds_AreCachedIndependently` the three calls become `FirstId`, `SecondId`, `FirstId`; `new AuthenticatedUser("5", 1)` and `new AuthenticatedUser("9", 1)` become `new AuthenticatedUser(SecondId, 1)` and `new AuthenticatedUser(FirstId, 1)`; `Assert.Equal("5", first!.Id)` / `Assert.Equal("5", second!.Id)` become `Assert.Equal(SecondId, first!.Id)` / `Assert.Equal(SecondId, second!.Id)`; and `provider.GetAuthenticatedUserById(9)` becomes `provider.GetAuthenticatedUserById(FirstId)`.

`tests/Security/AuthenticationMiddlewareTests.cs` — add the id constants next to `Secret`:

```csharp
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");
    private static readonly Guid OtherId = Guid.Parse("7b644d0a-9f11-4c5d-8e6a-4b903f2a9c1e");
```

the stub:

```csharp
    private sealed class StubProvider(IAuthenticatedUser? byId = null, IAuthenticatedUser? byEmail = null) : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => byId;
        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }
```

and the four user constructions plus their assertions: `new AuthenticatedUser("42", 3)` → `new AuthenticatedUser(UserId, 3)` with `Assert.Equal(UserId, user.Id)`; `new AuthenticatedUser("1", 1)` → `new AuthenticatedUser(UserId, 1)`; `new AuthenticatedUser("7", 2)` → `new AuthenticatedUser(OtherId, 2)` with `Assert.Equal(OtherId, user.Id)`; `new AuthenticatedUser("11", 1)` → `new AuthenticatedUser(OtherId, 1)` with `Assert.Equal(OtherId, Assert.IsType<AuthenticatedUser>(context.Items["User"]).Id)`.

`tests/Security/JwtTokenValidatorTests.cs` — add the id constant next to `Secret`:

```csharp
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");
```

the stub:

```csharp
    private sealed class StubProvider(IAuthenticatedUser? byId) : IAuthenticationProvider
    {
        public Guid? LastId { get; private set; }

        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
        {
            LastId = id;

            return byId;
        }

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }
```

every `new AuthenticatedUser("42", 3)` becomes `new AuthenticatedUser(UserId, 3)`, `new AuthenticatedUser("42", 9)` becomes `new AuthenticatedUser(UserId, 9)`, and `Assert.Equal("42", result.User!.Id)` becomes `Assert.Equal(UserId, result.User!.Id)`.

Replace the `Revalidate_LooksUpNonNumericIdFromClaims` test — a `Guid` is never numeric, so the name no longer says anything:

```csharp
    [Fact]
    public async Task Revalidate_PassesTokenIdToProvider()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3).ToTokenClaims());
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Equal(UserId, provider.LastId);
        Assert.Equal(UserId, result.User!.Id);
    }
```

and in `Revalidate_ReturnsError_WhenTokenHasNoIdClaim`, the stub construction becomes `new StubProvider(new AuthenticatedUser(UserId, 9))`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`
Expected: build failure. Representative errors: `CS1503: cannot convert from 'System.Guid' to 'string'` at the `AuthenticatedUser` constructions, `CS0535: 'StubProvider' does not implement interface member 'IAuthenticationProvider.GetAuthenticatedUserById(string)'`, and `CS0246`/`CS1503` around `IAuthenticatedUser` in the two filter test files.

Once the build succeeds (after Step 11), the two custom-user-type tests are the ones that must have been failing for behavioral reasons rather than compilation — that is the point of writing them now.

- [ ] **Step 4: Flip the record**

`src/Security/Records/AuthenticatedUser.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Interfaces;

namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The default <see cref="IAuthenticatedUser"/>, for apps that need nothing beyond an id and a role.
/// Attached to the current request after successful authentication.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="RoleId">The user's role id.</param>
public record AuthenticatedUser(Guid Id, int RoleId) : IAuthenticatedUser;
```

- [ ] **Step 5: Rename the role claim-key constant**

`src/Security/Constants/TokenClaimKeys.cs` — the string value stays `"role"`:

```csharp
namespace ArturRios.Util.WebApi.Security.Constants;

/// <summary>
/// The claim keys used by <see cref="Mappers.DefaultAuthenticatedUserMapper"/> to embed and read
/// authenticated-user data in JSON Web Tokens. A custom mapper may use any keys it likes.
/// </summary>
public static class TokenClaimKeys
{
    /// <summary>The claim key holding the user's id.</summary>
    public const string Id = "id";

    /// <summary>The claim key holding the user's role id.</summary>
    public const string RoleId = "role";
}
```

The `<see cref="Mappers.DefaultAuthenticatedUserMapper"/>` reference does not exist until Task 4 and will fail the build with CS1574. Use plain text for now — Task 4 Step 6 restores the `cref`:

```csharp
/// <summary>
/// The claim keys used by the library's default authenticated-user mapper to embed and read
/// authenticated-user data in JSON Web Tokens. A custom mapper may use any keys it likes.
/// </summary>
```

- [ ] **Step 6: Flip the provider interface**

`src/Security/Interfaces/IAuthenticationProvider.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Resolves an <see cref="IAuthenticatedUser"/> by id or email, used by the enabled <see cref="ITokenValidator"/>s
/// (via <see cref="Security.Middleware.AuthenticationMiddleware"/>) when validation requires a data-store lookup rather than trusting token claims alone.</summary>
public interface IAuthenticationProvider
{
    /// <summary>Looks up the authenticated user with the given id.</summary>
    /// <param name="id">The user id, typically read from the token by the app's mapper.</param>
    /// <returns>The matching user, or <c>null</c> if none was found.</returns>
    IAuthenticatedUser? GetAuthenticatedUserById(Guid id);

    /// <summary>Looks up the authenticated user with the given email, used when resolving an external (e.g. Google) identity.</summary>
    /// <param name="email">The verified email from the external token.</param>
    /// <returns>The matching user, or <c>null</c> if none was found.</returns>
    IAuthenticatedUser? GetAuthenticatedUserByEmail(string email);
}
```

Note the `using ArturRios.Util.WebApi.Security.Records;` line is gone — `IAuthenticatedUser` lives in this namespace.

- [ ] **Step 7: Flip the caching decorator**

In `src/Security/Providers/CachedAuthenticationProvider.cs`, change the two method signatures and the two `TryGetValue` type arguments. The `using ArturRios.Util.WebApi.Security.Records;` line is no longer needed and must be removed:

```csharp
    /// <inheritdoc />
    public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
    {
        var key = $"{_options.CacheKeyPrefix}{id}";

        if (cache.TryGetValue(key, out IAuthenticatedUser? cachedUser))
        {
            return cachedUser;
        }

        var user = inner.GetAuthenticatedUserById(id);
```

```csharp
    /// <inheritdoc />
    public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email)
    {
        var key = $"{_options.EmailCacheKeyPrefix}{email}";

        if (cache.TryGetValue(key, out IAuthenticatedUser? cachedUser))
        {
            return cachedUser;
        }

        var user = inner.GetAuthenticatedUserByEmail(email);
```

The rest of both method bodies is unchanged.

- [ ] **Step 8: Flip the validation result**

`src/Security/Records/TokenValidationResult.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Interfaces;

namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The outcome of an <c>ITokenValidator</c> attempt: a resolved user, or an error describing why validation failed.</summary>
/// <param name="User">The authenticated user when validation succeeded; otherwise <see langword="null"/>.</param>
/// <param name="Error">A human-readable error when <paramref name="User"/> is <see langword="null"/>; otherwise <see langword="null"/>.</param>
public readonly record struct TokenValidationResult(IAuthenticatedUser? User, string? Error);
```

- [ ] **Step 9: Update the two temporary claim helpers**

`src/Security/Extensions/AuthenticationExtensions.cs` — widened to the interface so tests can build tokens from any user; deleted in Task 5:

```csharp
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Interfaces;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Extension methods for converting authentication-related types.</summary>
public static class AuthenticationExtensions
{
    /// <summary>Converts an <see cref="IAuthenticatedUser"/> into the claim dictionary (keyed by <see cref="TokenClaimKeys"/>) used to build a JWT.</summary>
    /// <param name="authenticatedUser">The authenticated user to convert.</param>
    /// <returns>A dictionary containing the user's id and role claims.</returns>
    public static Dictionary<string, string> ToTokenClaims(this IAuthenticatedUser authenticatedUser) =>
        new()
        {
            { TokenClaimKeys.Id, authenticatedUser.Id.ToString() },
            { TokenClaimKeys.RoleId, authenticatedUser.RoleId.ToString() }
        };
}
```

`src/Security/Factories/AuthenticatedUserFactory.cs` — `Guid.TryParse` for the id, `TokenClaimKeys.RoleId` for the role, `Guid?` from `IdFromToken`. Replace the two public methods; `ReadClaims` and `ClaimValue` are unchanged:

```csharp
    /// <summary>
    /// Builds an <see cref="AuthenticatedUser"/> from the token's <c>id</c> and <c>role</c> claims.
    /// </summary>
    /// <param name="token">The JWT to read. Its signature is not validated here.</param>
    /// <returns>
    /// The reconstructed user, or <see langword="null"/> if the token cannot be read or its <c>id</c> or
    /// <c>role</c> claim is missing or unparseable.
    /// </returns>
    public static AuthenticatedUser? FromToken(string token)
    {
        var claims = ReadClaims(token);

        if (claims is null)
        {
            return null;
        }

        var idClaim = ClaimValue(claims, TokenClaimKeys.Id);
        var roleClaim = ClaimValue(claims, TokenClaimKeys.RoleId);

        if (!Guid.TryParse(idClaim, out var id) || !int.TryParse(roleClaim, out var roleId))
        {
            return null;
        }

        return new AuthenticatedUser(id, roleId);
    }

    /// <summary>
    /// Reads just the user id from the token's <c>id</c> claim, for callers that resolve the rest of the
    /// user from a data store.
    /// </summary>
    /// <param name="token">The JWT to read. Its signature is not validated here.</param>
    /// <returns>The user id, or <see langword="null"/> if the token cannot be read or its <c>id</c> claim is not a <see cref="Guid"/>.</returns>
    public static Guid? IdFromToken(string token)
    {
        var claims = ReadClaims(token);
        var idClaim = claims is null ? null : ClaimValue(claims, TokenClaimKeys.Id);

        return Guid.TryParse(idClaim, out var id) ? id : null;
    }
```

- [ ] **Step 10: Update the JWT validator's id handling**

In `src/Security/Authentication/JwtTokenValidator.cs`, `IdFromToken` now returns `Guid?`, so the null check and the lookup change:

```csharp
        var userId = AuthenticatedUserFactory.IdFromToken(token);

        if (userId is null)
        {
            return new TokenValidationResult(null, "Could not retrieve user id from token");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserById(userId.Value);
```

- [ ] **Step 11: Update the filter, the attribute, and the middleware**

`src/Security/Filters/RoleRequirementFilter.cs` — swap the `Records` using for `Constants` and `Interfaces`, then:

```csharp
        var user = context.HttpContext.Items[AuthenticationItemKeys.User] as IAuthenticatedUser;

        var authorized = false;

        if (user is not null)
        {
            authorized = user.RoleId.In(authorizedRoles);
        }
```

`src/Security/Attributes/AuthorizeAttribute.cs` — swap the `Records` using for `Constants` and `Interfaces`, update the class doc's `<see cref="AuthenticatedUser"/>` to `<see cref="IAuthenticatedUser"/>`, and replace the hard cast with a safe one:

```csharp
        var user = context.HttpContext.Items[AuthenticationItemKeys.User] as IAuthenticatedUser;
```

`src/Security/Middleware/AuthenticationMiddleware.cs` — add `using ArturRios.Util.WebApi.Security.Constants;` and use the constant:

```csharp
                context.Items[AuthenticationItemKeys.User] = user;
```

Note: `GoogleTokenValidator` needs **no** edit. It writes `var user = provider.GetAuthenticatedUserByEmail(...)`, which now infers `IAuthenticatedUser?` and flows into the widened `TokenValidationResult` unchanged. Do not go looking for a change there.

- [ ] **Step 12: Run the tests to verify they pass**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`
Expected: PASS, with 5 more tests than before this task (3 in `AuthorizeAttributeTests`, 2 in `RoleRequirementFilterTests`), plus the 5 added by Task 1.

- [ ] **Step 13: Verify a warning-free build**

Run: `dotnet build src/ArturRios.Util.WebApi.csproj`
Expected: `0 Warning(s), 0 Error(s)`. If CS1574 appears, a `<see cref="..."/>` still points at a type that does not exist yet — replace it with plain text as noted in Steps 3 and 5.

- [ ] **Step 14: Commit**

```bash
git add -A
git commit -m "feat!: identify users by Guid id and int role id

AuthenticatedUser implements IAuthenticatedUser, and the provider,
validators, filters and cache all speak the interface. Claim key
strings are unchanged so existing tokens still read."
```

---

### Task 3: TokenClaimsReader

Additive: the reader lands with its own tests while `AuthenticatedUserFactory` still serves the validator.

**Files:**
- Create: `src/Security/Authentication/TokenClaimsReader.cs`
- Test: `tests/Security/TokenClaimsReaderTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static IReadOnlyDictionary<string, string>? TokenClaimsReader.Read(string token)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Security/TokenClaimsReaderTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Authentication;

namespace ArturRios.Util.WebApi.Tests.Security;

public class TokenClaimsReaderTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    [Fact]
    public void Read_ReturnsClaims_FromReadableToken()
    {
        var token = CreateToken(new Dictionary<string, string> { { "id", "abc" }, { "tenant", "acme" } });

        var claims = TokenClaimsReader.Read(token);

        Assert.NotNull(claims);
        Assert.Equal("abc", claims["id"]);
        Assert.Equal("acme", claims["tenant"]);
    }

    [Fact]
    public void Read_ReturnsNull_WhenTokenIsNotReadable()
    {
        Assert.Null(TokenClaimsReader.Read("not-a-jwt"));
    }

    [Fact]
    public void Read_ReturnsNull_WhenTokenIsEmpty()
    {
        Assert.Null(TokenClaimsReader.Read(string.Empty));
    }

    [Fact]
    public void Read_KeepsFirstOccurrence_WhenClaimKeyIsRepeated()
    {
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(claims: [new Claim("dup", "first"), new Claim("dup", "second")]));

        var claims = TokenClaimsReader.Read(token);

        Assert.NotNull(claims);
        Assert.Equal("first", claims["dup"]);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --filter TokenClaimsReaderTests`
Expected: build failure — `CS0103: The name 'TokenClaimsReader' does not exist in the current context`.

- [ ] **Step 3: Write the reader**

`src/Security/Authentication/TokenClaimsReader.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>
/// Reads a JSON Web Token's claims into a dictionary, without validating its signature. Callers must
/// validate the signature before trusting the result.
/// </summary>
public static class TokenClaimsReader
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>Reads the token's claims. A repeated claim key keeps its first occurrence.</summary>
    /// <param name="token">The JWT to read. Its signature is not validated here.</param>
    /// <returns>The token's claims, or <see langword="null"/> if the token is blank or cannot be read.</returns>
    public static IReadOnlyDictionary<string, string>? Read(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Handler.CanReadToken(token))
        {
            return null;
        }

        var claims = new Dictionary<string, string>();

        foreach (var claim in Handler.ReadJwtToken(token).Claims)
        {
            claims.TryAdd(claim.Type, claim.Value);
        }

        return claims;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --filter TokenClaimsReaderTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Security/Authentication/TokenClaimsReader.cs tests/Security/TokenClaimsReaderTests.cs
git commit -m "feat: add TokenClaimsReader for reading JWT claims"
```

---

### Task 4: The mapper contract and its default implementation

Still additive — nothing consumes the mapper until Task 5.

**Files:**
- Create: `src/Security/Interfaces/IAuthenticatedUserMapper.cs`
- Create: `src/Security/Mappers/DefaultAuthenticatedUserMapper.cs`
- Modify: `src/Security/Constants/TokenClaimKeys.cs` (restore the `cref`)
- Test: `tests/Security/DefaultAuthenticatedUserMapperTests.cs`

**Interfaces:**
- Consumes: `IAuthenticatedUser` (Task 1), `AuthenticatedUser`, `TokenClaimKeys.Id`, `TokenClaimKeys.RoleId` (Task 2).
- Produces: `IAuthenticatedUserMapper` with `Dictionary<string, string> ToClaims(IAuthenticatedUser user)`, `IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)`, and `Guid? IdFromClaims(IReadOnlyDictionary<string, string> claims)` (default implementation delegating to `FromClaims`); `class DefaultAuthenticatedUserMapper : IAuthenticatedUserMapper`.

- [ ] **Step 1: Write the failing test**

Create `tests/Security/DefaultAuthenticatedUserMapperTests.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Mappers;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Security;

public class DefaultAuthenticatedUserMapperTests
{
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private static readonly DefaultAuthenticatedUserMapper Mapper = new();

    [Fact]
    public void ToClaims_WritesIdAndRoleId()
    {
        var claims = Mapper.ToClaims(new AuthenticatedUser(UserId, 3));

        Assert.Equal(UserId.ToString(), claims[TokenClaimKeys.Id]);
        Assert.Equal("3", claims[TokenClaimKeys.RoleId]);
    }

    [Fact]
    public void FromClaims_ReconstructsUser_FromClaimsWrittenByToClaims()
    {
        var user = Mapper.FromClaims(Mapper.ToClaims(new AuthenticatedUser(UserId, 3)));

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenIdIsNotAGuid()
    {
        var claims = new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, "not-a-guid" }, { TokenClaimKeys.RoleId, "3" }
        };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenRoleIdIsNotNumeric()
    {
        var claims = new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, UserId.ToString() }, { TokenClaimKeys.RoleId, "admin" }
        };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenIdClaimIsMissing()
    {
        var claims = new Dictionary<string, string> { { TokenClaimKeys.RoleId, "3" } };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenRoleIdClaimIsMissing()
    {
        var claims = new Dictionary<string, string> { { TokenClaimKeys.Id, UserId.ToString() } };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void IdFromClaims_ReturnsId_FromClaimsWrittenByToClaims()
    {
        var id = Mapper.IdFromClaims(Mapper.ToClaims(new AuthenticatedUser(UserId, 3)));

        Assert.Equal(UserId, id);
    }

    [Fact]
    public void IdFromClaims_ReturnsNull_WhenClaimsCannotProduceAUser()
    {
        Assert.Null(Mapper.IdFromClaims(new Dictionary<string, string>()));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --filter DefaultAuthenticatedUserMapperTests`
Expected: build failure — `CS0246: The type or namespace name 'Mappers' does not exist`.

- [ ] **Step 3: Write the mapper contract**

`src/Security/Interfaces/IAuthenticatedUserMapper.cs`:

```csharp
namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>
/// Translates between the app's <see cref="IAuthenticatedUser"/> and the claims carried by its JSON Web
/// Tokens. One implementation owns both directions, so the claims written when a token is issued and the
/// claims read when it is validated cannot drift apart.
/// </summary>
/// <remarks>Implementations must return <see langword="null"/> for claims they cannot interpret rather
/// than throwing. Use <c>TryParse</c>, not <c>Parse</c>.</remarks>
public interface IAuthenticatedUserMapper
{
    /// <summary>Builds the claims to embed in a token for the given user, called by the app at login.</summary>
    /// <param name="user">The user the token will represent.</param>
    /// <returns>The claims to embed, keyed by claim name.</returns>
    Dictionary<string, string> ToClaims(IAuthenticatedUser user);

    /// <summary>Rebuilds the user from a token's claims, without any data-store lookup.</summary>
    /// <param name="claims">The claims read from the token.</param>
    /// <returns>The user, or <see langword="null"/> if the claims cannot produce one.</returns>
    IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims);

    /// <summary>Reads just the user id from a token's claims, used when the rest of the user is resolved
    /// from the data store. Override this when the token carries more than <see cref="FromClaims"/> requires.</summary>
    /// <param name="claims">The claims read from the token.</param>
    /// <returns>The user id, or <see langword="null"/> if the claims do not carry one.</returns>
    Guid? IdFromClaims(IReadOnlyDictionary<string, string> claims) => FromClaims(claims)?.Id;
}
```

- [ ] **Step 4: Write the default mapper**

`src/Security/Mappers/DefaultAuthenticatedUserMapper.cs`:

```csharp
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Mappers;

/// <summary>
/// The mapper used when an app registers none of its own: it maps <see cref="IAuthenticatedUser.Id"/> and
/// <see cref="IAuthenticatedUser.RoleId"/> through <see cref="TokenClaimKeys"/> and produces an
/// <see cref="AuthenticatedUser"/>. Also the reference implementation for writing your own.
/// </summary>
public class DefaultAuthenticatedUserMapper : IAuthenticatedUserMapper
{
    /// <inheritdoc />
    public Dictionary<string, string> ToClaims(IAuthenticatedUser user) =>
        new()
        {
            { TokenClaimKeys.Id, user.Id.ToString() },
            { TokenClaimKeys.RoleId, user.RoleId.ToString() }
        };

    /// <inheritdoc />
    public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)
    {
        if (!claims.TryGetValue(TokenClaimKeys.Id, out var idClaim) || !Guid.TryParse(idClaim, out var id))
        {
            return null;
        }

        if (!claims.TryGetValue(TokenClaimKeys.RoleId, out var roleClaim) || !int.TryParse(roleClaim, out var roleId))
        {
            return null;
        }

        return new AuthenticatedUser(id, roleId);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj --filter DefaultAuthenticatedUserMapperTests`
Expected: PASS, 8 tests.

- [ ] **Step 6: Restore the claim-keys doc reference**

Now that the type exists, `src/Security/Constants/TokenClaimKeys.cs` can point at it:

```csharp
/// <summary>
/// The claim keys used by <see cref="Mappers.DefaultAuthenticatedUserMapper"/> to embed and read
/// authenticated-user data in JSON Web Tokens. A custom mapper may use any keys it likes.
/// </summary>
```

Run: `dotnet build src/ArturRios.Util.WebApi.csproj`
Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
git add src/Security/Interfaces/IAuthenticatedUserMapper.cs src/Security/Mappers/DefaultAuthenticatedUserMapper.cs src/Security/Constants/TokenClaimKeys.cs tests/Security/DefaultAuthenticatedUserMapperTests.cs
git commit -m "feat: add IAuthenticatedUserMapper and default mapper"
```

---

### Task 5: Rewire the validator and DI, delete the old claim paths

The payoff task: after it, no library code outside `DefaultAuthenticatedUserMapper` knows a claim key.

**Files:**
- Modify: `src/Security/Authentication/JwtTokenValidator.cs`
- Modify: `src/Security/Extensions/AuthenticationServiceCollectionExtensions.cs`
- Delete: `src/Security/Factories/AuthenticatedUserFactory.cs` (and the now-empty `src/Security/Factories/` folder)
- Delete: `src/Security/Extensions/AuthenticationExtensions.cs`
- Test: rewrite `tests/Security/JwtTokenValidatorTests.cs`; modify `tests/Security/AuthenticationMiddlewareTests.cs` and `tests/Security/AuthenticationServiceCollectionExtensionsTests.cs`; delete `tests/Security/AuthenticatedUserFactoryTests.cs`

**Interfaces:**
- Consumes: `TokenClaimsReader.Read` (Task 3), `IAuthenticatedUserMapper`, `DefaultAuthenticatedUserMapper` (Task 4), `IAuthenticationProvider`, `TokenValidationResult` (Task 2).
- Produces: `JwtTokenValidator(JwtConfiguration jwtConfig, JwtHandler jwtHandler, IAuthenticatedUserMapper mapper, AuthenticationOptions options)`; `AddTokenAuthentication<TMapper>(this IServiceCollection, Action<AuthenticationOptions>)` where `TMapper : class, IAuthenticatedUserMapper`; the existing non-generic `AddTokenAuthentication` overload delegating to it with `DefaultAuthenticatedUserMapper`.

- [ ] **Step 1: Rewrite the validator's tests (the failing state)**

Replace `tests/Security/JwtTokenValidatorTests.cs` entirely. The `TenantMapper` here is the point of the whole change: a caller-defined claim (`tenant`) and a caller-defined user type surviving a round-trip.

```csharp
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Mappers;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class JwtTokenValidatorTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";
    private const string TenantClaim = "tenant";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private sealed record TenantUser(Guid Id, int RoleId, string TenantId) : IAuthenticatedUser;

    private sealed class TenantMapper : IAuthenticatedUserMapper
    {
        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) =>
            new()
            {
                { TokenClaimKeys.Id, user.Id.ToString() },
                { TokenClaimKeys.RoleId, user.RoleId.ToString() },
                { TenantClaim, ((TenantUser)user).TenantId }
            };

        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)
        {
            if (!claims.TryGetValue(TokenClaimKeys.Id, out var idClaim) || !Guid.TryParse(idClaim, out var id) ||
                !claims.TryGetValue(TokenClaimKeys.RoleId, out var roleClaim) || !int.TryParse(roleClaim, out var roleId) ||
                !claims.TryGetValue(TenantClaim, out var tenantId))
            {
                return null;
            }

            return new TenantUser(id, roleId, tenantId);
        }
    }

    private sealed class NullMapper : IAuthenticatedUserMapper
    {
        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) => new();
        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims) => null;
    }

    private sealed class ThrowingMapper : IAuthenticatedUserMapper
    {
        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) => new();

        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims) =>
            throw new FormatException("bad claim");
    }

    private sealed class IdOnlyMapper : IAuthenticatedUserMapper
    {
        public bool FromClaimsCalled { get; private set; }

        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) =>
            new() { { TokenClaimKeys.Id, user.Id.ToString() } };

        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)
        {
            FromClaimsCalled = true;

            return null;
        }

        public Guid? IdFromClaims(IReadOnlyDictionary<string, string> claims) =>
            claims.TryGetValue(TokenClaimKeys.Id, out var id) && Guid.TryParse(id, out var userId) ? userId : null;
    }

    private sealed class StubProvider(IAuthenticatedUser? byId) : IAuthenticationProvider
    {
        public Guid? LastId { get; private set; }

        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
        {
            LastId = id;

            return byId;
        }

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
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

    private static JwtTokenValidator Validator(JwtValidationMode mode, IAuthenticatedUserMapper? mapper = null) =>
        new(Config(), new JwtHandler(), mapper ?? new DefaultAuthenticatedUserMapper(),
            new AuthenticationOptions { JwtMode = mode });

    [Fact]
    public async Task ClaimsOnly_ReturnsUserFromClaims()
    {
        var mapper = new DefaultAuthenticatedUserMapper();
        var token = CreateToken(mapper.ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
        Assert.Equal(UserId, result.User!.Id);
        Assert.Equal(3, result.User.RoleId);
    }

    [Fact]
    public async Task ClaimsOnly_ReturnsCallerType_WithCallerClaims()
    {
        var mapper = new TenantMapper();
        var token = CreateToken(mapper.ToClaims(new TenantUser(UserId, 3, "acme")));
        var result = await Validator(JwtValidationMode.ClaimsOnly, mapper).ValidateAsync(token, ContextWithProvider(null));

        var user = Assert.IsType<TenantUser>(result.User);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
        Assert.Equal("acme", user.TenantId);
    }

    [Fact]
    public async Task InvalidSignature_ReturnsError()
    {
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync("not-a-token", ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task ClaimsOnly_ReturnsError_WhenMapperReturnsNull()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.ClaimsOnly, new NullMapper())
            .ValidateAsync(token, ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user from token", result.Error);
    }

    [Fact]
    public async Task ClaimsOnly_ReturnsError_WhenMapperThrows()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.ClaimsOnly, new ThrowingMapper())
            .ValidateAsync(token, ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user from token", result.Error);
    }

    [Fact]
    public async Task Revalidate_ResolvesUserFromProvider()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Equal(UserId, provider.LastId);
        Assert.Equal(9, result.User!.RoleId);
    }

    [Fact]
    public async Task Revalidate_UsesOverriddenIdFromClaims_WithoutCallingFromClaims()
    {
        var mapper = new IdOnlyMapper();
        var token = CreateToken(mapper.ToClaims(new AuthenticatedUser(UserId, 3)));
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate, mapper).ValidateAsync(token, ContextWithProvider(provider));

        Assert.False(mapper.FromClaimsCalled);
        Assert.Equal(UserId, provider.LastId);
        Assert.Equal(9, result.User!.RoleId);
    }

    [Fact]
    public async Task Revalidate_ReturnsError_WhenTokenHasNoIdClaim()
    {
        var token = CreateToken(new Dictionary<string, string> { { TokenClaimKeys.RoleId, "3" } });
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user id from token", result.Error);
        Assert.Null(provider.LastId);
    }

    [Fact]
    public async Task Revalidate_ReturnsError_WhenMapperThrows()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.Revalidate, new ThrowingMapper())
            .ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user id from token", result.Error);
    }

    [Fact]
    public async Task Revalidate_ReturnsUserNotFound_WhenProviderReturnsNull()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }
}
```

`ThrowingMapper` inherits the default `IdFromClaims`, which calls `FromClaims`, which throws — that is what `Revalidate_ReturnsError_WhenMapperThrows` exercises.

- [ ] **Step 2: Point the other two test files at the mapper**

`tests/Security/AuthenticationMiddlewareTests.cs` — replace the `AuthenticationExtensions` using with the mapper's namespaces:

```csharp
using ArturRios.Util.WebApi.Security.Mappers;
```

and delete `using ArturRios.Util.WebApi.Security.Extensions;` — `ToTokenClaims` was the only thing this file used from that namespace.

add a shared mapper field next to `Secret`:

```csharp
    private static readonly DefaultAuthenticatedUserMapper Mapper = new();
```

update the `Jwt` helper to pass it:

```csharp
    private static ITokenValidator Jwt(AuthenticationOptions options) =>
        new JwtTokenValidator(Config(), new JwtHandler(), Mapper, options);
```

and replace each `new AuthenticatedUser(...).ToTokenClaims()` with `Mapper.ToClaims(new AuthenticatedUser(...))` — three occurrences (`UserId, 3`, `UserId, 1`, `OtherId, 1`).

`tests/Security/AuthenticationServiceCollectionExtensionsTests.cs` — add a test that the non-generic overload registers the default mapper, and one that the generic overload registers a custom one. Add `using ArturRios.Util.WebApi.Security.Mappers;`, a stub mapper alongside `FakeAuthenticationProvider`:

```csharp
    private sealed class StubMapper : IAuthenticatedUserMapper
    {
        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) => new();
        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims) => null;
    }
```

and the two tests:

```csharp
    [Fact]
    public void AddTokenAuthentication_RegistersDefaultMapper_WhenNoneSpecified()
    {
        var services = new ServiceCollection();
        services.AddTokenAuthentication(_ => { });

        var descriptor = Assert.Single(services.Where(d => d.ServiceType == typeof(IAuthenticatedUserMapper)));
        Assert.Equal(typeof(DefaultAuthenticatedUserMapper), descriptor.ImplementationType);
    }

    [Fact]
    public void AddTokenAuthentication_RegistersGivenMapper_WhenSpecified()
    {
        var services = new ServiceCollection();
        services.AddTokenAuthentication<StubMapper>(_ => { });

        var descriptor = Assert.Single(services.Where(d => d.ServiceType == typeof(IAuthenticatedUserMapper)));
        Assert.Equal(typeof(StubMapper), descriptor.ImplementationType);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`
Expected: build failure — `CS1729: 'JwtTokenValidator' does not contain a constructor that takes 4 arguments` and `CS0311`/`CS1061` on `AddTokenAuthentication<StubMapper>`.

- [ ] **Step 4: Rewire the validator**

Replace `src/Security/Authentication/JwtTokenValidator.cs` entirely:

```csharp
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Validates the app's own HMAC-signed JWT and resolves the user through the registered
/// <see cref="IAuthenticatedUserMapper"/> — from claims alone or by an <see cref="IAuthenticationProvider"/>
/// lookup, per <see cref="AuthenticationOptions.JwtMode"/>.</summary>
/// <param name="jwtConfig">Provides the signing secret used to validate the token.</param>
/// <param name="jwtHandler">Validates token signatures.</param>
/// <param name="mapper">Interprets the token's claims as the app's user.</param>
/// <param name="options">Controls how the user is resolved once the signature is valid.</param>
public class JwtTokenValidator(
    JwtConfiguration jwtConfig,
    JwtHandler jwtHandler,
    IAuthenticatedUserMapper mapper,
    AuthenticationOptions options) : ITokenValidator
{
    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        var isValid = await jwtHandler.IsTokenValidAsync(token, jwtConfig.Secret);

        if (!isValid)
        {
            return new TokenValidationResult(null, "Invalid token");
        }

        var claims = TokenClaimsReader.Read(token);

        if (claims is null)
        {
            return new TokenValidationResult(null, "Could not read token claims");
        }

        if (options.JwtMode == JwtValidationMode.ClaimsOnly)
        {
            var claimsUser = MapUser(claims);

            return new TokenValidationResult(claimsUser, claimsUser is null ? "Could not retrieve user from token" : null);
        }

        var userId = MapId(claims);

        if (userId is null)
        {
            return new TokenValidationResult(null, "Could not retrieve user id from token");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserById(userId.Value);

        return new TokenValidationResult(user, user is null ? "User not found" : null);
    }

    private IAuthenticatedUser? MapUser(IReadOnlyDictionary<string, string> claims)
    {
        try
        {
            return mapper.FromClaims(claims);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Guid? MapId(IReadOnlyDictionary<string, string> claims)
    {
        try
        {
            return mapper.IdFromClaims(claims);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

The two `try`/`catch` wrappers are the security backstop from the spec: a mapper written with `Guid.Parse` must not let a hand-crafted token become a 500. They wrap only the mapper calls.

- [ ] **Step 5: Add the mapper to registration**

In `src/Security/Extensions/AuthenticationServiceCollectionExtensions.cs`, add `using ArturRios.Util.WebApi.Security.Mappers;`, then replace the `AddTokenAuthentication` method with an overload pair:

```csharp
    /// <summary>
    /// Registers the consolidated <see cref="AuthenticationOptions"/>, the enabled token validators, and
    /// <see cref="DefaultAuthenticatedUserMapper"/> as the claims mapper. Use
    /// <see cref="AddTokenAuthentication{TMapper}"/> to supply your own mapper instead.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Configures the options.</param>
    /// <exception cref="ArgumentException">No scheme enabled, or Google enabled without any client IDs.</exception>
    public static IServiceCollection AddTokenAuthentication(
        this IServiceCollection services, Action<AuthenticationOptions> configure) =>
        services.AddTokenAuthentication<DefaultAuthenticatedUserMapper>(configure);

    /// <summary>
    /// Registers the consolidated <see cref="AuthenticationOptions"/>, the enabled token validators, and
    /// <typeparamref name="TMapper"/> as the claims mapper. Validators are registered app-JWT first, Google
    /// second, so the middleware tries them in that order. The app must separately register
    /// <c>JwtConfiguration</c> and <c>JwtHandler</c> (for JWT) and an <see cref="IAuthenticationProvider"/>
    /// (required for Google and for JWT <c>Revalidate</c> mode).
    /// </summary>
    /// <typeparam name="TMapper">The mapper translating between the app's user and its token claims.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Configures the options.</param>
    /// <exception cref="ArgumentException">No scheme enabled, or Google enabled without any client IDs.</exception>
    public static IServiceCollection AddTokenAuthentication<TMapper>(
        this IServiceCollection services, Action<AuthenticationOptions> configure)
        where TMapper : class, IAuthenticatedUserMapper
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
        services.TryAddSingleton<IAuthenticatedUserMapper, TMapper>();

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

`TryAddSingleton` lets an app register its own mapper before calling this. `Microsoft.Extensions.DependencyInjection.Extensions` is already imported in this file.

- [ ] **Step 6: Delete the superseded claim paths**

```bash
git rm src/Security/Factories/AuthenticatedUserFactory.cs src/Security/Extensions/AuthenticationExtensions.cs tests/Security/AuthenticatedUserFactoryTests.cs
```

`src/Security/Factories/` is now empty; `git rm` leaves no tracked files there, so nothing else is needed. The factory's test file goes with it — `DefaultAuthenticatedUserMapperTests` (Task 4) already covers the same parsing rules against the type that replaces it.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`
Expected: PASS. If `CS0246` mentions `AuthenticationExtensions` or `AuthenticatedUserFactory`, a `using` or call site was missed — search with `git grep -n "ToTokenClaims\|AuthenticatedUserFactory"` and fix.

- [ ] **Step 8: Verify a warning-free build**

Run: `dotnet build src/ArturRios.Util.WebApi.csproj`
Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat!: map token claims through a caller-defined mapper

JwtTokenValidator now reads claims with TokenClaimsReader and hands
them to the registered IAuthenticatedUserMapper, which also builds the
claims at login. AuthenticatedUserFactory and ToTokenClaims are gone."
```

---

### Task 6: Documentation and version bump

**Files:**
- Modify: `src/ArturRios.Util.WebApi.csproj:10`
- Modify: `docs/content/security.md`
- Modify: `docs/content/architecture.md:60-61` and the prose at `:86-87`
- Modify: `README.md:112` and `:123`

**Interfaces:**
- Consumes: every public name produced by Tasks 1–5.
- Produces: nothing consumed by code.

- [ ] **Step 1: Bump the package version**

`src/ArturRios.Util.WebApi.csproj` line 10 — breaking change, so major:

```xml
    <Version>3.0.0</Version>
```

- [ ] **Step 2: Rewrite the identity section of `docs/content/security.md`**

Replace the whole `## Identity types` section (the `AuthenticatedUser`/`TokenClaimKeys`/`ToTokenClaims`/`AuthenticatedUserFactory` bullet list and the mermaid diagram that follows it) with:

````markdown
## Identity types

The library knows exactly two things about a user — an id and a role id — and leaves everything else,
including the token's claim keys, to the app.

- **`IAuthenticatedUser`** — `Guid Id` and `int RoleId`. Implement it on your own type, adding whatever
  else your app needs; instances are attached to the current request after successful authentication and
  returned by both `IAuthenticationProvider` methods.
- **`AuthenticatedUser(Guid Id, int RoleId)`** — the default implementation, for apps that need nothing
  extra.
- **`IAuthenticatedUserMapper`** — translates between your user and your claims, in both directions:
  `ToClaims(IAuthenticatedUser)` builds the claims you embed at login, `FromClaims(...)` rebuilds the user
  during `ClaimsOnly` validation, and `IdFromClaims(...)` reads just the id for `Revalidate` mode
  (a default implementation delegates to `FromClaims`; override it when your token carries more than
  `FromClaims` needs). Implementations must return `null` for claims they cannot interpret rather than
  throwing — use `TryParse`, not `Parse`.
- **`DefaultAuthenticatedUserMapper`** — used when you register no mapper of your own. Maps `Id`/`RoleId`
  through `TokenClaimKeys` and produces an `AuthenticatedUser`.
- **`TokenClaimKeys`** — the claim keys the default mapper uses: `Id = "id"`, `RoleId = "role"`. A custom
  mapper may use any keys it likes.
- **`TokenClaimsReader.Read(string token)`** — reads a token's claims into a dictionary without validating
  its signature (callers must have already done that), or `null` if the token can't be read. A repeated
  claim key keeps its first occurrence.
- **`HttpContext.GetUser<TUser>()`** — the authenticated user as your own type, or `null` if the request
  isn't authenticated or the attached user is another type. `GetUser()` returns `IAuthenticatedUser?`.

```csharp
public record MyUser(Guid Id, int RoleId, string TenantId) : IAuthenticatedUser;

public class MyUserMapper : IAuthenticatedUserMapper
{
    private const string TenantClaim = "tenant";

    public Dictionary<string, string> ToClaims(IAuthenticatedUser user) =>
        new()
        {
            { TokenClaimKeys.Id, user.Id.ToString() },
            { TokenClaimKeys.RoleId, user.RoleId.ToString() },
            { TenantClaim, ((MyUser)user).TenantId }
        };

    public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)
    {
        if (!claims.TryGetValue(TokenClaimKeys.Id, out var id) || !Guid.TryParse(id, out var userId) ||
            !claims.TryGetValue(TokenClaimKeys.RoleId, out var role) || !int.TryParse(role, out var roleId))
        {
            return null;
        }

        claims.TryGetValue(TenantClaim, out var tenantId);

        return new MyUser(userId, roleId, tenantId ?? string.Empty);
    }
}
```

Register it with the generic overload; omit the type argument to get `DefaultAuthenticatedUserMapper`:

```csharp
builder.Services.AddTokenAuthentication<MyUserMapper>(options => { /* ... */ });
```

```mermaid
flowchart LR
    User["your IAuthenticatedUser"] -- "mapper.ToClaims()" --> Claims["your claims"]
    Claims -- "embedded in JWT at login" --> Token["Bearer token"]
    Token -- "TokenClaimsReader.Read()" --> Claims2["claims dictionary"]
    Claims2 -- "mapper.FromClaims()" --> User2["your IAuthenticatedUser"]
```

Because `ToClaims` takes the interface, a mapper writing extra claims casts to its own type. The cast is
safe: the same app owns the user type, the mapper, and the provider that produced the user.
````

- [ ] **Step 3: Update the rest of `docs/content/security.md`**

Four smaller edits in the same file:

1. In the `ClaimsOnly` bullet, replace `AuthenticatedUserFactory.FromToken` with `the registered mapper's FromClaims`.
2. In the `Revalidate` bullet, replace `the user id is read from the token` with `the user id is read from the token by the mapper's IdFromClaims`.
3. In the caching section, the sentence `GetAuthenticatedUserById checks the cache first (key "auth:user:" + id ...)` is still correct — leave it.
4. In the numbered "To accept Google sign-in" list, `IAuthenticationProvider.GetAuthenticatedUserByEmail(string)` is unchanged — leave it.

- [ ] **Step 4: Update `docs/content/architecture.md`**

Two flow nodes and one sentence:

```mermaid
    Mode -- "ClaimsOnly (default)" --> Claims["mapper.FromClaims<br/><i>your claims, no lookup</i>"]
    Mode -- "Revalidate" --> ProviderId["IAuthenticationProvider.GetAuthenticatedUserById<br/><i>Guid id, resolved per-request from RequestServices</i>"]
```

and in the prose below it, replace `calls GetAuthenticatedUserById on every request` with `calls GetAuthenticatedUserById with the Guid from the token's claims on every request`.

- [ ] **Step 5: Update `README.md`**

In the security bullet list, replace the `Revalidate` bullet's `IAuthenticationProvider.GetAuthenticatedUserById is called on every request` with `IAuthenticationProvider.GetAuthenticatedUserById(Guid) is called on every request`, and add one bullet after it:

```markdown
- **Your own identity** — implement `IAuthenticatedUser` (`Guid Id`, `int RoleId`) on your own type and an
  `IAuthenticatedUserMapper` to decide what your tokens carry; read it back with
  `HttpContext.GetUser<MyUser>()`. See [Security](https://artur-rios.github.io/dotnet-webapi-util/security/).
```

- [ ] **Step 6: Verify the docs match the code**

Run: `git grep -n "ToTokenClaims\|AuthenticatedUserFactory\|user\.Role\b\|GetAuthenticatedUserById(int\|GetAuthenticatedUserById(string" -- README.md docs/content src tests`
Expected: no matches. (`docs/superpowers/` and `docs/coverage-report/` are historical records and are expected to still mention the old names — do not edit them.)

- [ ] **Step 7: Final verification**

Run: `dotnet build src/ArturRios.Util.WebApi.csproj` then `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj`
Expected: `0 Warning(s), 0 Error(s)`; all tests pass.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "docs: document caller-defined identity and bump to 3.0.0"
```

---

## Verification summary

After Task 6, all of the following must hold:

- `dotnet build src/ArturRios.Util.WebApi.csproj` → 0 errors, 0 warnings.
- `dotnet test tests/ArturRios.Util.WebApi.Tests.csproj` → all green.
- `git grep -n "ToTokenClaims\|AuthenticatedUserFactory"` finds nothing under `src`, `tests`, `README.md`, or `docs/content`.
- No library file outside `src/Security/Mappers/DefaultAuthenticatedUserMapper.cs` references `TokenClaimKeys`.
- `src/Security/Factories/` no longer exists.
