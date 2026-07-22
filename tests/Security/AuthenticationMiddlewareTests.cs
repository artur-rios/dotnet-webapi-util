using System.Text;
using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Attributes;
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
        Assert.Contains("Invalid token", await ReadBody(context));
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
        var google = new GoogleTokenValidator(new FakeVerifier(new GoogleTokenPayload("u@e.com", "sub", true)), options);
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
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), "anon");
        context.SetEndpoint(endpoint);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options)]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
    }
}
