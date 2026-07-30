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
