using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

[Trait("Category", "Unit")]
public class GoogleTokenValidatorTests
{
    private static readonly Guid UserId = Guid.Parse("7b644d0a-9f11-4c5d-8e6a-4b903f2a9c1e");

    private sealed class FakeVerifier(GoogleTokenPayload? payload) : IGoogleTokenVerifier
    {
        public Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences) => Task.FromResult(payload);
    }

    private sealed class StubProvider(IAuthenticatedUser? byEmail) : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => null;
        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => byEmail;
    }

    private static HttpContext ContextWithProvider(IAuthenticationProvider provider) =>
        new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider() };

    private static AuthenticationOptions Options() =>
        new() { EnableGoogle = true, GoogleClientIds = { "client-id-1" } };

    [Fact]
    public async Task GivenAValidGoogleToken_WhenValidating_ThenTheUserIsResolvedByEmail()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("user@example.com", "google-sub-1", true));
        var provider = new StubProvider(new AuthenticatedUser(UserId, 2));
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(provider));

        Assert.NotNull(result.User);
        Assert.Equal(UserId, result.User!.Id);
    }

    [Fact]
    public async Task GivenAValidGoogleTokenForAnUnknownEmail_WhenValidating_ThenUserNotFoundIsReported()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("nobody@example.com", "google-sub-2", true));
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.Equal("User not found", result.Error);
    }

    [Fact]
    public async Task GivenATokenTheVerifierRejects_WhenValidating_ThenAnErrorIsReported()
    {
        var verifier = new FakeVerifier(payload: null); // verifier rejected audience/issuer/expiry/signature
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("bad.token", ContextWithProvider(new StubProvider(null)));

        Assert.Null(result.User);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task GivenAnUnverifiedEmail_WhenValidating_ThenItIsRejectedBeforeAnyUserLookup()
    {
        var verifier = new FakeVerifier(new GoogleTokenPayload("user@example.com", "google-sub-3", false));
        var provider = new StubProvider(new AuthenticatedUser(UserId, 2)); // would resolve a user if lookup were reached
        var validator = new GoogleTokenValidator(verifier, Options());

        var result = await validator.ValidateAsync("google.id.token", ContextWithProvider(provider));

        Assert.Null(result.User);
        Assert.Equal("Google email not verified", result.Error);
    }
}
