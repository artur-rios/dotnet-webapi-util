using System.Net;
using System.Text.Json;
using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
using ArturRios.Output;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Mappers;
using ArturRios.Util.WebApi.Security.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArturRios.Util.WebApi.Tests.Functional;

/// <summary>
/// Runs the trace, exception and authentication middlewares together in a real ASP.NET Core host and
/// drives real HTTP requests through it, so the ordering, the response envelopes and the headers are
/// exercised the way an application would meet them.
/// </summary>
[Trait("Category", "Functional")]
public sealed class WebApiPipelineTests : IAsyncLifetime
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private IHost _host = null!;
    private HttpClient _client = null!;

    private sealed record User(Guid Id, int RoleId) : IAuthenticatedUser;

    private sealed class KnownUserProvider : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => id == UserId ? new User(UserId, 1) : null;

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    public async Task InitializeAsync()
    {
        var options = new AuthenticationOptions { JwtMode = JwtValidationMode.ClaimsOnly, Source = TokenSource.Header };
        var jwtConfiguration = new JwtConfiguration(3600, "issuer", "audience", Secret, new Dictionary<string, string>());

        _host = await new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddSingleton(new SettingsProvider(new ConfigurationBuilder().Build()));
                    services.AddSingleton(options);
                    services.AddSingleton(jwtConfiguration);
                    services.AddSingleton<JwtHandler>();
                    services.AddSingleton<IAuthenticatedUserMapper, DefaultAuthenticatedUserMapper>();
                    services.AddSingleton<IAuthenticationProvider, KnownUserProvider>();
                    services.AddSingleton<ITokenValidator>(provider => new JwtTokenValidator(
                        jwtConfiguration,
                        provider.GetRequiredService<JwtHandler>(),
                        provider.GetRequiredService<IAuthenticatedUserMapper>(),
                        options));
                })
                .Configure(app =>
                {
                    app.UseMiddleware<TraceActivityMiddleware>();
                    app.UseMiddleware<ExceptionMiddleware>();

                    app.Map("/open", branch => branch.Run(context => context.Response.WriteAsync("open")));

                    app.Map("/boom", branch => branch
                        .UseMiddleware<ExceptionMiddleware>()
                        .Run(_ => throw new InvalidOperationException("internal detail")));

                    app.Map("/secure", branch => branch
                        .UseMiddleware<AuthenticationMiddleware>()
                        .Run(async context =>
                        {
                            var user = context.GetUser();

                            await context.Response.WriteAsync(user?.Id.ToString() ?? "none");
                        }));
                }))
            .StartAsync();

        _client = _host.GetTestClient();

        return;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();

        await _host.StopAsync();

        _host.Dispose();
    }

    private static string TokenFor(Guid id) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret,
            new DefaultAuthenticatedUserMapper().ToClaims(new User(id, 1))));

    [Fact]
    public async Task GivenAnyRequest_WhenItPassesThroughTheHost_ThenTheResponseCarriesATraceparentHeader()
    {
        var response = await _client.GetAsync("/open");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("traceparent"));

        var traceparent = response.Headers.GetValues("traceparent").Single();

        Assert.StartsWith("00-", traceparent);
        Assert.Equal(4, traceparent.Split('-').Length);
    }

    [Fact]
    public async Task GivenTwoRequests_WhenBothPassThroughTheHost_ThenEachCarriesItsOwnTraceId()
    {
        var first = await _client.GetAsync("/open");
        var second = await _client.GetAsync("/open");

        Assert.NotEqual(
            first.Headers.GetValues("traceparent").Single(),
            second.Headers.GetValues("traceparent").Single());
    }

    [Fact]
    public async Task GivenAnEndpointThatThrows_WhenCalled_ThenA500ArrivesWithAFailedEnvelope()
    {
        var response = await _client.GetAsync("/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(await response.Content.ReadAsStringAsync())!;

        Assert.False(output.Success);
        Assert.Contains(output.Errors, error => error.Contains("Internal server error"));
        Assert.DoesNotContain(output.Errors, error => error.Contains("internal detail"));
    }

    [Fact]
    public async Task GivenNoToken_WhenCallingASecuredEndpoint_ThenUnauthorizedArrivesWithAFailedEnvelope()
    {
        var response = await _client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var output = JsonSerializer.Deserialize<ProcessOutput>(await response.Content.ReadAsStringAsync())!;

        Assert.False(output.Success);
        Assert.NotEmpty(output.Errors);
    }

    [Fact]
    public async Task GivenAValidToken_WhenCallingASecuredEndpoint_ThenTheUserReachesTheEndpoint()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/secure");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenFor(UserId));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserId.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GivenATokenSignedWithAnotherSecret_WhenCallingASecuredEndpoint_ThenUnauthorizedArrives()
    {
        var foreignToken = new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience",
            "a-completely-different-secret-of-sufficient-length-000",
            new DefaultAuthenticatedUserMapper().ToClaims(new User(UserId, 1))));

        var request = new HttpRequestMessage(HttpMethod.Get, "/secure");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", foreignToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenAGarbageAuthorizationHeader_WhenCallingASecuredEndpoint_ThenUnauthorizedArrivesWithoutThrowing()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/secure");

        request.Headers.TryAddWithoutValidation("Authorization", "Bearer a.b.c");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
