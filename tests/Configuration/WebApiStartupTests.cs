using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArturRios.Configuration.Providers;
using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Extensions;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArturRios.Util.WebApi.Tests.Configuration;

/// <summary>
/// Drives the standard sequence <see cref="WebApiStartup"/> builds the application with through a real test host,
/// so configuration, services, middleware order and the controllers are exercised together; and Build, Run and
/// RunAsync themselves.
/// </summary>
[Trait("Category", "Functional")]
public sealed class WebApiStartupTests : WebApiStartupTestHost
{
    /// <summary>A startup that takes the standard options, and whose application stops itself once it has started,
    /// so Run and RunAsync return.</summary>
    private sealed class SelfStoppingStartup(string[] args) : WebApiStartup(args)
    {
        public StartedSignal Signal { get; } = new();

        protected override void ConfigureServices(WebApplicationBuilder builder)
        {
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(Signal);
            builder.Services.AddHostedService<StopOnceStarted>();
        }
    }

    private sealed class StartedSignal
    {
        public bool Started { get; set; }
    }

    private sealed class StopOnceStarted(IHostApplicationLifetime lifetime, StartedSignal signal) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                signal.Started = true;
                lifetime.StopApplication();
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static readonly string[] SelfStoppingArgs = ["Environment:Production", "UseAppSettings:false", "UseEnvFile:false"];

    [Fact]
    public async Task GivenTheStandardSequence_WhenAControllerIsCalled_ThenItAnswersWithATraceparent()
    {
        var client = await Start();

        var response = await client.GetAsync("/samples", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.Contains("traceparent"));
    }

    [Fact]
    public async Task GivenTheStandardSequence_WhenAControllerThrows_ThenA500EnvelopeArrives()
    {
        var client = await Start();

        var response = await client.GetAsync("/samples/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.DoesNotContain(output.Errors, error => error.Contains("internal detail"));
    }

    [Fact]
    public async Task GivenTheStandardSequence_WhenTheModelIsInvalid_ThenA400EnvelopeListsTheParameter()
    {
        var client = await Start();

        var response = await client.PostAsJsonAsync("/samples", new { }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.Contains(output.Errors, error => error.Contains("Name"));
    }

    [Fact]
    public async Task GivenTheEnvelopeIsTurnedOff_WhenTheModelIsInvalid_ThenAspNetCoresProblemDetailsArrive()
    {
        var client = await Start(configureOptions: options => options.UseInvalidModelStateEnvelope = false);

        var response = await client.PostAsJsonAsync("/samples", new { }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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

        Assert.Equal("198.51.100.9", await (await client.SendAsync(request, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenForwardedHeadersAreNotConfigured_WhenAClientSendsXForwardedFor_ThenItIsIgnored()
    {
        var client = await Start(
            configureApp: app => app.MapGet("/ip", (HttpContext context) => context.GetClientIpAddress() ?? "none"));

        var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", "198.51.100.9");

        Assert.Equal("none", await (await client.SendAsync(request, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
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
    public async Task GivenAnExtraMiddlewareInTheOptions_WhenARequestIsMade_ThenItRuns()
    {
        var client = await Start(configureOptions: options => options.Middlewares.Add(typeof(MarkerMiddleware)));

        var response = await client.GetAsync("/samples", TestContext.Current.CancellationToken);

        Assert.Equal("1", response.Headers.GetValues("X-Marker").Single());
    }

    [Fact]
    public async Task GivenTheBuiltApplication_WhenAnEndpointIsMappedOnIt_ThenItIsServedAlongTheControllers()
    {
        var client = await Start(configureApp: app => app.MapGet("/health", () => "healthy"));

        Assert.Equal("healthy", await client.GetStringAsync("/health", TestContext.Current.CancellationToken));
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

        Apps.Add(startup.Build());

        Assert.Throws<InvalidOperationException>(() => startup.Build());
    }

    [Fact]
    public void GivenAnEnvironmentArgument_WhenBuilding_ThenTheApplicationRunsInThatEnvironment()
    {
        var startup = new TestStartup(["Environment:Staging", "UseAppSettings:false", "UseEnvFile:false"],
            _ => { }, _ => { });

        var app = startup.Build();
        Apps.Add(app);

        Assert.Equal("Staging", app.Environment.EnvironmentName);
        Assert.Same(startup.PublicParameters, app.Services.GetRequiredService<WebApiParameters>());
        Assert.NotNull(app.Services.GetService<SettingsProvider>());
    }

    [Fact]
    public async Task GivenAStartup_WhenRunAsynchronously_ThenTheApplicationStartsAndRunsUntilItIsStopped()
    {
        var startup = new SelfStoppingStartup(SelfStoppingArgs);

        await startup.RunAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(startup.Signal.Started);
    }

    [Fact]
    public async Task GivenAStartup_WhenRun_ThenTheApplicationStartsAndRunsUntilItIsStopped()
    {
        var startup = new SelfStoppingStartup(SelfStoppingArgs);

        await Task.Run(startup.Run, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(startup.Signal.Started);
    }

    [Fact]
    public void GivenTheStartupWithTheStandardOptions_WhenBuilt_ThenTheInvalidModelStateEnvelopeIsOn()
    {
        var startup = new SelfStoppingStartup(SelfStoppingArgs);

        var app = startup.Build();
        Apps.Add(app);

        var factory = app.Services.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory;
        var context = new ActionContext(new DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        context.ModelState.AddModelError("Name", "required");

        Assert.IsType<DataOutput<string>>(Assert.IsType<BadRequestObjectResult>(factory(context)).Value);
    }

    [Fact]
    public void GivenANullConfigureCallback_WhenTheStartupIsCreated_ThenItThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestStartup(["UseAppSettings:false", "UseEnvFile:false"], null!, _ => { }));
    }
}
