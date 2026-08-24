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

[Trait("Category", "Unit")]
public class JwtTokenValidatorTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

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
    public async Task GivenClaimsOnlyMode_WhenValidatingAValidToken_ThenTheUserComesFromTheClaims()
    {
        var mapper = new DefaultAuthenticatedUserMapper();
        var token = CreateToken(mapper.ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
        Assert.Equal(UserId, result.User!.Id);
        Assert.Equal(3, result.User.RoleId);
    }

    [Fact]
    public async Task GivenClaimsOnlyModeAndACallerMapper_WhenValidating_ThenTheCallersOwnUserTypeComesBack()
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
    public async Task GivenATokenWithAnInvalidSignature_WhenValidating_ThenAnErrorIsReported()
    {
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync("not-a-token", ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task GivenAMapperThatReturnsNull_WhenValidatingInClaimsOnlyMode_ThenAnErrorIsReported()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.ClaimsOnly, new NullMapper())
            .ValidateAsync(token, ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user from token", result.Error);
    }

    [Fact]
    public async Task GivenAMapperThatThrows_WhenValidatingInClaimsOnlyMode_ThenAnErrorIsReported()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.ClaimsOnly, new ThrowingMapper())
            .ValidateAsync(token, ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user from token", result.Error);
    }

    [Fact]
    public async Task GivenRevalidateMode_WhenValidating_ThenTheUserIsResolvedFromTheProvider()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Equal(UserId, provider.LastId);
        Assert.Equal(9, result.User!.RoleId);
    }

    [Fact]
    public async Task GivenAMapperOverridingIdFromClaims_WhenValidatingInRevalidateMode_ThenOnlyThatOverrideIsUsed()
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
    public async Task GivenATokenWithNoIdClaim_WhenValidatingInRevalidateMode_ThenAnErrorIsReported()
    {
        var token = CreateToken(new Dictionary<string, string> { { TokenClaimKeys.RoleId, "3" } });
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user id from token", result.Error);
        Assert.Null(provider.LastId);
    }

    [Fact]
    public async Task GivenAMapperThatThrows_WhenValidatingInRevalidateMode_ThenAnErrorIsReported()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.Revalidate, new ThrowingMapper())
            .ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user id from token", result.Error);
    }

    [Fact]
    public async Task GivenAProviderThatFindsNoUser_WhenValidatingInRevalidateMode_ThenUserNotFoundIsReported()
    {
        var token = CreateToken(new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3)));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }

    // Key rotation (ArturRios.Jwt 1.1.0). A configuration carrying Keys validates against all of
    // them, so a token signed with a key that is no longer the signing key survives until that key is
    // withdrawn — the difference between rotating a signing secret and cutting over to a new one.

    private const string PreviousSecret = "the-previous-signing-key-with-enough-length-1234567890";

    private static readonly JwtKey PreviousKey = new("k1", PreviousSecret);
    private static readonly JwtKey CurrentKey = new("k2", Secret);

    private static JwtConfiguration RotatingConfig(params JwtKey[] keys) =>
        Config() with { Keys = keys, SigningKeyId = keys.LastOrDefault()?.Id };

    private static string CreateToken(JwtConfiguration configuration, Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(configuration with { Claims = claims });

    private static JwtTokenValidator Validator(JwtConfiguration configuration) =>
        new(configuration, new JwtHandler(), new DefaultAuthenticatedUserMapper(),
            new AuthenticationOptions { JwtMode = JwtValidationMode.ClaimsOnly });

    [Fact]
    public async Task GivenConfiguredKeys_WhenValidatingATokenSignedWithTheCurrentKey_ThenItIsAccepted()
    {
        var claims = new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3));
        var configuration = RotatingConfig(PreviousKey, CurrentKey);
        var token = CreateToken(configuration, claims);

        var result = await Validator(configuration).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
        Assert.Equal(UserId, result.User!.Id);
    }

    [Fact]
    public async Task GivenConfiguredKeys_WhenValidatingATokenSignedWithARetiredButAcceptedKey_ThenItIsAccepted()
    {
        // The point of the feature: this token was issued before the rotation, and its holder must
        // not be signed out by it.
        var claims = new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3));
        var issuedBefore = CreateToken(RotatingConfig(PreviousKey), claims);

        var result = await Validator(RotatingConfig(PreviousKey, CurrentKey))
            .ValidateAsync(issuedBefore, ContextWithProvider(null));

        Assert.NotNull(result.User);
    }

    [Fact]
    public async Task GivenConfiguredKeys_WhenValidatingATokenSignedWithAWithdrawnKey_ThenItIsRejected()
    {
        // And the half that matters when a key has leaked rather than merely aged.
        var claims = new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3));
        var issuedBefore = CreateToken(RotatingConfig(PreviousKey), claims);

        var result = await Validator(RotatingConfig(CurrentKey))
            .ValidateAsync(issuedBefore, ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.Equal("Invalid token", result.Error);
    }

    [Fact]
    public async Task GivenConfiguredKeys_WhenValidatingATokenIssuedBeforeRotation_ThenItIsAccepted()
    {
        // No kid, because it was signed by a configuration that had no Keys. Adopting rotation must
        // not invalidate what was issued before it.
        var claims = new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3));
        var legacyToken = CreateToken(claims);

        var result = await Validator(RotatingConfig(PreviousKey, CurrentKey))
            .ValidateAsync(legacyToken, ContextWithProvider(null));

        Assert.NotNull(result.User);
    }

    [Fact]
    public async Task GivenNoConfiguredKeys_WhenValidating_ThenTheSingleSecretIsStillUsed()
    {
        // The fallback every configuration written before this took. It has its own test because the
        // branch that chooses it is the one thing standing between those consumers and a break.
        var claims = new DefaultAuthenticatedUserMapper().ToClaims(new AuthenticatedUser(UserId, 3));
        var token = CreateToken(claims);

        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
    }
}
