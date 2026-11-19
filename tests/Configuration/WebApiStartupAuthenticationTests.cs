using System.Net;
using ArturRios.Jwt;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Configuration;

/// <summary>
/// Token authentication in the standard sequence: when it runs, what it lets through, and the issuer and audience
/// checks.
/// </summary>
[Trait("Category", "Functional")]
public sealed class WebApiStartupAuthenticationTests : WebApiStartupTestHost
{
    [Fact]
    public async Task GivenTokenAuthenticationIsRegistered_WhenASecuredActionIsCalledWithoutAToken_ThenUnauthorizedArrives()
    {
        var client = await Start(configureServices: AddJwtAuthentication);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/samples/secure", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/samples", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task GivenACorsPolicyAndTokenAuthentication_WhenAPreflightIsSent_ThenItPassesWithoutAToken()
    {
        var client = await Start(
            configureOptions: options => options.CorsPolicy = "frontend",
            configureServices: builder =>
            {
                AddJwtAuthentication(builder);
                builder.Services.AddCors(cors => cors.AddPolicy("frontend", policy => policy
                    .WithOrigins("https://app.example.test").AllowAnyHeader().AllowAnyMethod()));
            });

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/samples/secure");
        preflight.Headers.Add("Origin", "https://app.example.test");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(preflight, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://app.example.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    // The README's quick start maps health checks next to token authentication: a probe sends no token.
    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task GivenTokenAuthentication_WhenAHealthCheckIsProbedWithoutAToken_ThenOnlyAnAnonymousOneAnswers(
        bool allowAnonymous, HttpStatusCode expected)
    {
        var client = await Start(
            configureServices: builder =>
            {
                AddJwtAuthentication(builder);
                builder.Services.AddHealthChecks();
            },
            configureApp: app =>
            {
                var health = app.MapHealthChecks("/health");

                if (allowAnonymous)
                {
                    health.AllowAnonymous();
                }
            });

        Assert.Equal(expected, (await client.GetAsync("/health", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task GivenNoTokenAuthenticationIsRegistered_WhenASecuredActionIsCalled_ThenNoAuthenticationRuns()
    {
        var client = await Start();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/samples/secure", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task GivenValidateIssuerWithNoConfiguredIssuer_WhenTheAppStarts_ThenStartupFailsWithAClearError()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(configureServices: builder =>
            AddJwtAuthenticationWith(builder, issuer: string.Empty, configure: options => options.ValidateIssuer = true)));

        Assert.Contains("ValidateIssuer", exception.Message);
        Assert.Contains("JwtConfiguration.Issuer", exception.Message);
    }

    [Fact]
    public async Task GivenValidateAudienceWithNoConfiguredAudience_WhenTheAppStarts_ThenStartupFailsWithAClearError()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(configureServices: builder =>
            AddJwtAuthenticationWith(builder, audience: " ", configure: options => options.ValidateAudience = true)));

        Assert.Contains("ValidateAudience", exception.Message);
        Assert.Contains("JwtConfiguration.Audience", exception.Message);
    }

    [Theory]
    [InlineData("issuer", "audience", HttpStatusCode.OK)]
    [InlineData("someone-else", "audience", HttpStatusCode.Unauthorized)]
    [InlineData("issuer", "another-app", HttpStatusCode.Unauthorized)]
    public async Task GivenIssuerAndAudienceChecks_WhenATokenIsSent_ThenOnlyOneForThisIssuerAndAudienceIsAccepted(
        string issuer, string audience, HttpStatusCode expected)
    {
        var client = await Start(configureServices: builder => AddJwtAuthenticationWith(builder, configure: options =>
        {
            options.ValidateIssuer = true;
            options.ValidateAudience = true;
        }));

        var token = new JwtHandler().CreateToken(new JwtConfiguration(3600, issuer, audience, Secret,
            new Dictionary<string, string> { { "id", Guid.NewGuid().ToString() }, { "role", "1" } }));
        var request = new HttpRequestMessage(HttpMethod.Get, "/samples/secure");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(expected, (await client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task GivenDefaultAuthenticationOptions_WhenATokenForAnotherIssuerAndAudienceIsSent_ThenItIsAccepted()
    {
        var client = await Start(configureServices: AddJwtAuthentication);

        var token = new JwtHandler().CreateToken(new JwtConfiguration(3600, "someone-else", "another-app", Secret,
            new Dictionary<string, string> { { "id", Guid.NewGuid().ToString() }, { "role", "1" } }));
        var request = new HttpRequestMessage(HttpMethod.Get, "/samples/secure");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
    }
}
