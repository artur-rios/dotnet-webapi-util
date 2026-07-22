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

    private sealed class StubProvider(AuthenticatedUser? byId) : IAuthenticationProvider
    {
        public AuthenticatedUser? GetAuthenticatedUserById(int id) => byId;
        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
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
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var result = await Validator(JwtValidationMode.ClaimsOnly).ValidateAsync(token, ContextWithProvider(null));

        Assert.NotNull(result.User);
        Assert.Equal(42, result.User!.Id);
        Assert.Equal(3, result.User.Role);
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
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var provider = new StubProvider(new AuthenticatedUser(42, 9));
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(provider));

        Assert.NotNull(result.User);
        Assert.Equal(9, result.User!.Role);
    }

    [Fact]
    public async Task Revalidate_ReturnsUserNotFound_WhenProviderReturnsNull()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3).ToTokenClaims());
        var result = await Validator(JwtValidationMode.Revalidate).ValidateAsync(token, ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }
}
