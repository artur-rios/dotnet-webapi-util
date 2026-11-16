using System.ComponentModel.DataAnnotations;
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
/// What the <see cref="WebApiStartup"/> tests share: a startup that runs on a test server, and the applications each
/// test starts, disposed after it. Every test configures its own application, so the tests are split by topic into
/// classes of their own, which xUnit runs in parallel.
/// </summary>
public abstract class WebApiStartupTestHost : IAsyncDisposable
{
    protected const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    protected List<WebApplication> Apps { get; } = [];

    protected sealed class TestStartup(
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

    protected async Task<HttpClient> Start(string environment = "Production",
        Action<WebApiStartupOptions>? configureOptions = null, Action<WebApplicationBuilder>? configureServices = null,
        Action<WebApplication>? configureApp = null)
    {
        var startup = new TestStartup(
            [$"Environment:{environment}", "UseAppSettings:false", "UseEnvFile:false"],
            configureOptions ?? (_ => { }),
            configureServices ?? (_ => { }));

        var app = startup.Build();

        configureApp?.Invoke(app);

        Apps.Add(app);

        await app.StartAsync();

        return app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var app in Apps)
        {
            await app.DisposeAsync();
        }
    }

    protected static void AddJwtAuthentication(WebApplicationBuilder builder) => AddJwtAuthenticationWith(builder);

    protected static void AddJwtAuthenticationWith(WebApplicationBuilder builder, string issuer = "issuer",
        string audience = "audience", Action<AuthenticationOptions>? configure = null)
    {
        builder.Services.AddSingleton(
            new JwtConfiguration(3600, issuer, audience, Secret, new Dictionary<string, string>()));
        builder.Services.AddSingleton<JwtHandler>();
        builder.Services.AddTokenAuthentication(configure ?? (_ => { }));
    }
}
