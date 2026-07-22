using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class GoogleTokenValidatorTests
{
    private sealed class FakeVerifier(GoogleTokenPayload? payload) : IGoogleTokenVerifier
    {
        public Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences) => Task.FromResult(payload);
    }

    private sealed class StubProvider(AuthenticatedUser? byEmail) : IAuthenticationProvider
    {
        public AuthenticatedUser? GetAuthenticatedUserById(int id) => null;
        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }

    private static HttpContext ContextWithProvider(IAuthenticationProvider provider) =>
        new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider() };

    private static AuthenticationOptions Options() =>
        new() { EnableGoogle = true, GoogleClientIds = { "client-id-1" } };

    [Fact]
    public async Task ValidToken_ResolvesUserByEmail()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("user@example.com", "google-sub-1", true));
        var provider = new StubProvider(new AuthenticatedUser(7, 2));
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(provider));

        Assert.NotNull(result.User);
        Assert.Equal(7, result.User!.Id);
    }

    [Fact]
    public async Task ValidToken_UnknownEmail_ReturnsUserNotFound()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("nobody@example.com", "google-sub-2", true));
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }

    [Fact]
    public async Task RejectedToken_ReturnsError()
    {
        var verifier = new FakeVerifier(payload: null); // verifier rejected audience/issuer/expiry/signature
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("bad.token", ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task ValidToken_UnverifiedEmail_RejectsBeforeUserLookup()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("user@example.com", "google-sub-3", false));
        var provider = new StubProvider(new AuthenticatedUser(7, 2)); // would resolve a user if lookup were reached
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(provider));

        Assert.Null(result.User);
        Assert.Equal("Google email not verified", result.Error);
    }
}
