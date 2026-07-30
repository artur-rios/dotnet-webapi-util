using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class JwtTokenValidatorTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

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

    private static JwtTokenValidator Validator(JwtValidationMode mode) =>
        new(Config(), new JwtHandler(), new AuthenticationOptions { JwtMode = mode });

    [Fact]
    public async Task ClaimsOnly_ReturnsUserFromClaims()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3).ToTokenClaims());
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
        Assert.Equal(UserId, result.User!.Id);
        Assert.Equal(3, result.User.RoleId);
    }

    [Fact]
    public async Task InvalidSignature_ReturnsError()
    {
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync("not-a-token", ContextWithProvider(null));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task Revalidate_ResolvesUserFromProvider()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3).ToTokenClaims());
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.NotNull(result.User);
        Assert.Equal(9, result.User!.RoleId);
    }

    [Fact]
    public async Task Revalidate_PassesTokenIdToProvider()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3).ToTokenClaims());
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Equal(UserId, provider.LastId);
        Assert.Equal(UserId, result.User!.Id);
    }

    [Fact]
    public async Task Revalidate_ReturnsError_WhenTokenHasNoIdClaim()
    {
        var token = CreateToken(new Dictionary<string, string> { { "role", "3" } });
        var provider = new StubProvider(new AuthenticatedUser(UserId, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.Null(result.User);
        Assert.Equal("Could not retrieve user id from token", result.Error);
        Assert.Null(provider.LastId);
    }

    [Fact]
    public async Task Revalidate_ReturnsUserNotFound_WhenProviderReturnsNull()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3).ToTokenClaims());
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }
}
