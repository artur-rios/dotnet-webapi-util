using System.Text;
using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Mappers;
using ArturRios.Util.WebApi.Security.Middleware;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthenticationMiddlewareTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");
    private static readonly Guid OtherId = Guid.Parse("7b644d0a-9f11-4c5d-8e6a-4b903f2a9c1e");
    private static readonly DefaultAuthenticatedUserMapper Mapper = new();

    private sealed class StubProvider(IAuthenticatedUser? byId = null, IAuthenticatedUser? byEmail = null) : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => byId;
        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }

    private sealed class FakeVerifier(GoogleTokenPayload? payload) : IGoogleTokenVerifier
    {
        public Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences) => Task.FromResult(payload);
    }

    private sealed record TenantUser(Guid Id, int RoleId, string TenantId) : IAuthenticatedUser;

    private sealed class TenantMapper : IAuthenticatedUserMapper
    {
        private const string IdClaim = "uid";
        private const string RoleClaim = "r";
        private const string TenantClaim = "tenant";

        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) =>
            new()
            {
                { IdClaim, user.Id.ToString() },
                { RoleClaim, user.RoleId.ToString() },
                { TenantClaim, ((TenantUser)user).TenantId }
            };

        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)
        {
            if (!claims.TryGetValue(IdClaim, out var idClaim) || !Guid.TryParse(idClaim, out var id) ||
                !claims.TryGetValue(RoleClaim, out var roleClaim) || !int.TryParse(roleClaim, out var roleId) ||
                !claims.TryGetValue(TenantClaim, out var tenantId))
            {
                return null;
            }

            return new TenantUser(id, roleId, tenantId);
        }
    }

    private static SettingsProvider EmptySettings() => new(new ConfigurationBuilder().Build());

    private static JwtConfiguration Config() => new(3600, "issuer", "audience", Secret, new Dictionary<string, string>());

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    private static AuthenticationMiddleware Middleware(
        RequestDelegate next, AuthenticationOptions options, IEnumerable<ITokenValidator> validators) =>
        new(next, EmptySettings(), options, validators);

    private static ITokenValidator Jwt(AuthenticationOptions options) =>
        new JwtTokenValidator(Config(), new JwtHandler(), Mapper, options);

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
        var token = CreateToken(Mapper.ToClaims(new AuthenticatedUser(UserId, 3)));
        var (context, log) = BuildContext(token, provider: null);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options)]);

        await middleware.InvokeAsync(context);

        var user = Assert.IsType<AuthenticatedUser>(context.Items["User"]);
        Assert.Equal(UserId, user.Id);
        Assert.Equal("next", log.ToString());
    }

    [Fact]
    public async Task Jwt_CustomMapper_AttachesCallerTypeReadableByGetUser()
    {
        var options = new AuthenticationOptions { JwtMode = JwtValidationMode.ClaimsOnly };
        var mapper = new TenantMapper();
        var token = CreateToken(mapper.ToClaims(new TenantUser(UserId, 3, "acme")));
        var (context, log) = BuildContext(token, provider: null);
        var validator = new JwtTokenValidator(Config(), new JwtHandler(), mapper, options);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [validator]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        Assert.Equal("acme", context.GetUser<TenantUser>()!.TenantId);
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
        var token = CreateToken(Mapper.ToClaims(new AuthenticatedUser(UserId, 1)));
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
        var provider = new StubProvider(byEmail: new AuthenticatedUser(OtherId, 2));
        var (context, log) = BuildContext("google.id.token", provider);
        var google = new GoogleTokenValidator(new FakeVerifier(new GoogleTokenPayload("u@e.com", "sub", true)), options);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options), google]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        var user = Assert.IsType<AuthenticatedUser>(context.Items["User"]);
        Assert.Equal(OtherId, user.Id);
    }

    [Fact]
    public async Task BothEnabled_AcceptsAppJwt()
    {
        var options = new AuthenticationOptions { EnableGoogle = true, GoogleClientIds = { "cid" } };
        var token = CreateToken(Mapper.ToClaims(new AuthenticatedUser(OtherId, 1)));
        var (context, log) = BuildContext(token, provider: null);
        var google = new GoogleTokenValidator(new FakeVerifier(payload: null), options);
        var middleware = Middleware(_ => { log.Append("next"); return Task.CompletedTask; }, options, [Jwt(options), google]);

        await middleware.InvokeAsync(context);

        Assert.Equal("next", log.ToString());
        Assert.Equal(OtherId, Assert.IsType<AuthenticatedUser>(context.Items["User"]).Id);
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
