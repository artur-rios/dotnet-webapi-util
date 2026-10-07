using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Extensions;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArturRios.Util.WebApi.Tests.Configuration;

/// <summary>The controller the standard pipeline is driven through. Public and top-level so MVC discovers it.</summary>
[ApiController]
[Route("samples")]
public class StartupSampleController : ControllerBase
{
    public sealed class SampleInput
    {
        [Required]
        public string? Name { get; set; }
    }

    [HttpGet]
    [AllowAnonymous]
    public string Get() => "ok";

    [HttpGet("secure")]
    public string Secure() => "secret";

    [HttpGet("boom")]
    [AllowAnonymous]
    public string Boom() => throw new InvalidOperationException("internal detail");

    [HttpPost]
    [AllowAnonymous]
    public string Post(SampleInput input) => input.Name!;
}

/// <summary>Adds a marker header, so a test can tell an extra middleware from the options ran.</summary>
public class MarkerMiddleware(RequestDelegate next) : WebApiMiddleware
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Marker"] = "1";

        return next(context);
    }
}

/// <summary>
/// Drives the standard sequence <see cref="WebApiStartup"/> builds the application with through a real test host,
/// so configuration, services, middleware order, Swagger and the controllers are exercised together.
/// </summary>
[Trait("Category", "Functional")]
public sealed class WebApiStartupTests : IAsyncLifetime
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private readonly List<WebApplication> _apps = [];

    private sealed class TestStartup(
        string[] args,
        Action<WebApiStartupOptions> configureOptions,
        Action<WebApplicationBuilder> configureServices) : WebApiStartup(args, configureOptions)
    {
        public WebApiParameters PublicParameters => Parameters;

        protected override void ConfigureServices(WebApplicationBuilder builder)
        {
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(StartupSampleController).Assembly);

            configureServices(builder);
        }
    }

    private async Task<HttpClient> Start(string environment = "Production",
        Action<WebApiStartupOptions>? configureOptions = null, Action<WebApplicationBuilder>? configureServices = null,
        Action<WebApplication>? configureApp = null)
    {
        var startup = new TestStartup(
            [$"Environment:{environment}", "UseAppSettings:false", "UseEnvFile:false"],
            configureOptions ?? (_ => { }),
            configureServices ?? (_ => { }));

        var app = startup.Build();

        configureApp?.Invoke(app);

        _apps.Add(app);

        await app.StartAsync();

        return app.GetTestClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var app in _apps)
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task GivenTheStandardSequence_WhenAControllerIsCalled_ThenItAnswersWithATraceparent()
    {
        var client = await Start();

        var response = await client.GetAsync("/samples");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("traceparent"));
    }

    [Fact]
    public async Task GivenTheStandardSequence_WhenAControllerThrows_ThenA500EnvelopeArrives()
    {
        var client = await Start();

        var response = await client.GetAsync("/samples/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(
            await response.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.DoesNotContain(output.Errors, error => error.Contains("internal detail"));
    }

    [Fact]
    public async Task GivenTheStandardSequence_WhenTheModelIsInvalid_ThenA400EnvelopeListsTheParameter()
    {
        var client = await Start();

        var response = await client.PostAsJsonAsync("/samples", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(
            await response.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.Contains(output.Errors, error => error.Contains("Name"));
    }

    [Fact]
    public async Task GivenTheEnvelopeIsTurnedOff_WhenTheModelIsInvalid_ThenAspNetCoresProblemDetailsArrive()
    {
        var client = await Start(configureOptions: options => options.UseInvalidModelStateEnvelope = false);

        var response = await client.PostAsJsonAsync("/samples", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GivenTheDevelopmentEnvironment_WhenTheSwaggerDocumentIsRequested_ThenItIsServed()
    {
        var client = await Start("Development");

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenTheProductionEnvironment_WhenTheSwaggerDocumentIsRequested_ThenItIsNotServed()
    {
        var client = await Start("Production");

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GivenSwaggerAllowedInProductionByTheOptions_WhenTheDocumentIsRequested_ThenItIsServed()
    {
        var client = await Start("Production",
            options => options.Swagger.AllowedEnvironments = [ArturRios.Configuration.Enums.EnvironmentType.Production]);

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenTokenAuthenticationIsRegistered_WhenASecuredActionIsCalledWithoutAToken_ThenUnauthorizedArrives()
    {
        var client = await Start(configureServices: AddJwtAuthentication);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/samples/secure")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/samples")).StatusCode);
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

        var response = await client.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://app.example.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task GivenTokenAuthenticationAndSwaggerInDevelopment_WhenTheDocumentIsRequested_ThenNoTokenIsNeeded()
    {
        var client = await Start("Development", configureServices: AddJwtAuthentication);

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenForwardedHeadersAreConfigured_WhenAProxyForwardsTheClient_ThenTheClientIpIsTheForwardedOne()
    {
        var client = await Start(
            configureServices: builder => builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }),
            configureApp: app => app.MapGet("/ip", (HttpContext context) => context.GetClientIpAddress() ?? "none"));

        var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", "198.51.100.9");

        Assert.Equal("198.51.100.9", await (await client.SendAsync(request)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GivenForwardedHeadersAreNotConfigured_WhenAClientSendsXForwardedFor_ThenItIsIgnored()
    {
        var client = await Start(
            configureApp: app => app.MapGet("/ip", (HttpContext context) => context.GetClientIpAddress() ?? "none"));

        var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", "198.51.100.9");

        Assert.Equal("none", await (await client.SendAsync(request)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GivenClientIpLoggingIsDisabledInTheOptions_WhenTheAppIsBuilt_ThenTheMiddlewareOptionsReflectIt()
    {
        TraceActivityOptions? resolved = null;

        await Start(
            configureOptions: options =>
            {
                options.TraceActivity.LogClientIp = false;
                options.TraceActivity.TagClientAddress = false;
            },
            configureApp: app => resolved = app.Services.GetRequiredService<IOptions<TraceActivityOptions>>().Value);

        Assert.NotNull(resolved);
        Assert.False(resolved.LogClientIp);
        Assert.False(resolved.TagClientAddress);
    }

    [Fact]
    public async Task GivenNoTokenAuthenticationIsRegistered_WhenASecuredActionIsCalled_ThenNoAuthenticationRuns()
    {
        var client = await Start();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/samples/secure")).StatusCode);
    }

    [Fact]
    public async Task GivenAnExtraMiddlewareInTheOptions_WhenARequestIsMade_ThenItRuns()
    {
        var client = await Start(configureOptions: options => options.Middlewares.Add(typeof(MarkerMiddleware)));

        var response = await client.GetAsync("/samples");

        Assert.Equal("1", response.Headers.GetValues("X-Marker").Single());
    }

    [Fact]
    public async Task GivenTheBuiltApplication_WhenAnEndpointIsMappedOnIt_ThenItIsServedAlongTheControllers()
    {
        var client = await Start(configureApp: app => app.MapGet("/health", () => "healthy"));

        Assert.Equal("healthy", await client.GetStringAsync("/health"));
    }

    [Fact]
    public void GivenAnExtraMiddlewareThatIsNotAWebApiMiddleware_WhenBuilding_ThenItFails()
    {
        var startup = new TestStartup(["UseAppSettings:false", "UseEnvFile:false"],
            options => options.Middlewares.Add(typeof(string)), _ => { });

        Assert.Throws<ArgumentException>(() => startup.Build());
    }

    [Fact]
    public void GivenTheApplicationWasBuilt_WhenBuildingAgain_ThenItFails()
    {
        var startup = new TestStartup(["UseAppSettings:false", "UseEnvFile:false"], _ => { }, _ => { });

        _apps.Add(startup.Build());

        Assert.Throws<InvalidOperationException>(() => startup.Build());
    }

    [Fact]
    public void GivenAnEnvironmentArgument_WhenBuilding_ThenTheApplicationRunsInThatEnvironment()
    {
        var startup = new TestStartup(["Environment:Staging", "UseAppSettings:false", "UseEnvFile:false"],
            _ => { }, _ => { });

        var app = startup.Build();
        _apps.Add(app);

        Assert.Equal("Staging", app.Environment.EnvironmentName);
        Assert.Same(startup.PublicParameters, app.Services.GetRequiredService<WebApiParameters>());
        Assert.NotNull(app.Services.GetService<SettingsProvider>());
    }

    [Fact]
    public void GivenSwaggerIsAllowed_WhenBuilding_ThenTheSwaggerMarkerIsSetForTheAuthenticationBypass()
    {
        var startup = new TestStartup(["Environment:Development", "UseAppSettings:false", "UseEnvFile:false"],
            _ => { }, _ => { });

        var app = startup.Build();
        _apps.Add(app);

        var settings = new SettingsProvider(app.Services.GetRequiredService<IConfiguration>());
        Assert.True(settings.GetBool(AppSettingsKeys.SwaggerEnabled));
    }

    private static void AddJwtAuthentication(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(
            new JwtConfiguration(3600, "issuer", "audience", Secret, new Dictionary<string, string>()));
        builder.Services.AddSingleton<JwtHandler>();
        builder.Services.AddTokenAuthentication(_ => { });
    }
}
