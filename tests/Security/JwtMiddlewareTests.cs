using System.Text;
using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
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

public class JwtMiddlewareTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private sealed class CountingAuthenticationProvider(AuthenticatedUser? user) : IAuthenticationProvider
    {
        public int CallCount { get; private set; }

        public AuthenticatedUser? GetAuthenticatedUserById(int id)
        {
            CallCount++;

            return user;
        }

        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    private static SettingsProvider EmptySettings() =>
        new(new ConfigurationBuilder().Build());

    private static string CreateToken(Dictionary<string, string> claims)
    {
        var handler = new JwtHandler();
        var configuration = new JwtConfiguration(3600, "issuer", "audience", Secret, claims);

        return handler.CreateToken(configuration);
    }

    private static (DefaultHttpContext Context, StringBuilder Next) BuildContext(
        string? token, IAuthenticationProvider? provider)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (token is not null)
        {
            context.Request.Headers.Authorization = $"Bearer {token}";
        }

        if (provider is not null)
        {
            context.RequestServices = new ServiceCollection()
                .AddSingleton(provider)
                .BuildServiceProvider();
        }

        return (context, new StringBuilder());
    }

    private static JwtMiddleware CreateMiddleware(RequestDelegate next, JwtValidationMode mode)
    {
        var tokenConfig = new JwtConfiguration(3600, "issuer", "audience", Secret, new Dictionary<string, string>());

        return new JwtMiddleware(
            next,
            EmptySettings(),
            tokenConfig,
            new JwtHandler(),
            new JwtAuthenticationOptions { ValidationMode = mode });
    }

    private static async Task<string> ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(context.Response.Body);

        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task ClaimsOnly_SetsUserFromToken_AndCallsNext()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var (context, log) = BuildContext(token, provider: null);
        var middleware = CreateMiddleware(_ =>
        {
            log.Append("next");

            return Task.CompletedTask;
        }, JwtValidationMode.ClaimsOnly);

        await middleware.InvokeAsync(context);

        var user = Assert.IsType<AuthenticatedUser>(context.Items["User"]);
        Assert.Equal(42, user.Id);
        Assert.Equal(3, user.Role);
        Assert.Equal("next", log.ToString());
    }

    [Fact]
    public async Task ClaimsOnly_DoesNotQueryAuthenticationProvider()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var provider = new CountingAuthenticationProvider(new AuthenticatedUser(42, 3));
        var (context, _) = BuildContext(token, provider);
        var middleware = CreateMiddleware(_ => Task.CompletedTask, JwtValidationMode.ClaimsOnly);

        await middleware.InvokeAsync(context);

        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ClaimsOnly_ReturnsUnauthorized_WhenTokenLacksUserClaims()
    {
        var token = CreateToken(new Dictionary<string, string>());
        var (context, log) = BuildContext(token, provider: null);
        var middleware = CreateMiddleware(_ =>
        {
            log.Append("next");

            return Task.CompletedTask;
        }, JwtValidationMode.ClaimsOnly);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Empty(log.ToString());
    }

    [Fact]
    public async Task Revalidate_ResolvesUserFromProvider_AndCallsNext()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var provider = new CountingAuthenticationProvider(new AuthenticatedUser(42, 9));
        var (context, log) = BuildContext(token, provider);
        var middleware = CreateMiddleware(_ =>
        {
            log.Append("next");

            return Task.CompletedTask;
        }, JwtValidationMode.Revalidate);

        await middleware.InvokeAsync(context);

        Assert.Equal(1, provider.CallCount);
        var user = Assert.IsType<AuthenticatedUser>(context.Items["User"]);
        Assert.Equal(9, user.Role);
        Assert.Equal("next", log.ToString());
    }

    [Fact]
    public async Task Revalidate_ReturnsUnauthorized_WhenProviderReturnsNull()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var provider = new CountingAuthenticationProvider(user: null);
        var (context, log) = BuildContext(token, provider);
        var middleware = CreateMiddleware(_ =>
        {
            log.Append("next");

            return Task.CompletedTask;
        }, JwtValidationMode.Revalidate);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("User not found", await ReadBody(context));
        Assert.Empty(log.ToString());
    }

    [Fact]
    public async Task Activates_ViaDependencyInjection_WithoutRegisteredOptions_DefaultingToClaimsOnly()
    {
        // Mirrors how UseMiddleware<JwtMiddleware> builds the instance: RequestDelegate is passed
        // explicitly and the remaining constructor parameters are resolved from DI. Options are not
        // registered here, so activation must fall back to the default (ClaimsOnly) and query no provider.
        var provider = new CountingAuthenticationProvider(new AuthenticatedUser(42, 3));
        var services = new ServiceCollection()
            .AddSingleton(EmptySettings())
            .AddSingleton(new JwtConfiguration(3600, "issuer", "audience", Secret, new Dictionary<string, string>()))
            .AddSingleton(new JwtHandler())
            .AddSingleton<IAuthenticationProvider>(provider)
            .BuildServiceProvider();

        var log = new StringBuilder();
        RequestDelegate next = _ =>
        {
            log.Append("next");

            return Task.CompletedTask;
        };

        var middleware = ActivatorUtilities.CreateInstance<JwtMiddleware>(services, next);

        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = $"Bearer {token}";

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ReturnsUnauthorized_WhenTokenSignatureInvalid()
    {
        var (context, log) = BuildContext("not-a-valid-token", provider: null);
        var middleware = CreateMiddleware(_ =>
        {
            log.Append("next");

            return Task.CompletedTask;
        }, JwtValidationMode.ClaimsOnly);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Empty(log.ToString());
    }

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
}
