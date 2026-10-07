using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>
/// Base class that bootstraps an ASP.NET Core web API through one standard sequence. A derived class only registers
/// its own services in <see cref="ConfigureServices"/>; the rest runs in this order:
/// <list type="number">
/// <item><see cref="WebApplicationBuilderExtensions.LoadConfiguration"/>, per the command-line <see cref="Parameters"/>.</item>
/// <item>Controllers, the invalid-model-state envelope (<see cref="ServiceCollectionExtensions.AddInvalidModelStateEnvelope"/>)
/// and Swagger (<see cref="WebApplicationBuilderExtensions.AddWebApiSwagger"/>), as tuned by <see cref="WebApiStartupOptions"/>.</item>
/// <item><see cref="ConfigureServices"/>.</item>
/// <item>The pipeline: <see cref="ApplicationBuilderExtensions.UseStandardMiddlewares"/> (tracing, exceptions, Swagger,
/// CORS, authentication), the extra <see cref="WebApiStartupOptions.Middlewares"/>, then the controllers.</item>
/// </list>
/// <see cref="Build"/> returns the <see cref="WebApplication"/>, so endpoints beyond the controllers (health checks,
/// minimal APIs) are mapped on it before it runs.
/// </summary>
public abstract class WebApiStartup
{
    private bool _built;

    /// <summary>Parses <paramref name="args"/> and creates the builder, using the standard options.</summary>
    /// <param name="args">The command-line arguments passed to the application entry point.</param>
    protected WebApiStartup(string[] args) : this(args, _ => { }) { }

    /// <summary>Parses <paramref name="args"/> and creates the builder, tuning the standard sequence with
    /// <paramref name="configure"/>.</summary>
    /// <param name="args">The command-line arguments passed to the application entry point.</param>
    /// <param name="configure">Tunes Swagger, CORS, the invalid-model-state envelope and the extra middlewares.</param>
    protected WebApiStartup(string[] args, Action<WebApiStartupOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Parameters = new WebApiParameters(args);
        Builder = WebApplication.CreateBuilder(Parameters.ToWebApplicationOptions());

        configure(Options);
    }

    /// <summary>The startup parameters parsed from the command-line arguments.</summary>
    protected WebApiParameters Parameters { get; }

    /// <summary>The options tuning the standard sequence.</summary>
    protected WebApiStartupOptions Options { get; } = new();

    /// <summary>The builder the application is built from. Its environment is
    /// <see cref="WebApiParameters.EnvironmentName"/> when one was supplied.</summary>
    protected WebApplicationBuilder Builder { get; }

    /// <summary>Registers the application's own services: data access, handlers, token authentication, CORS, hosted
    /// services, and so on. Runs after the standard services and before the application is built.</summary>
    /// <param name="builder">The builder, exposing <c>Services</c>, <c>Configuration</c> and <c>Environment</c>.</param>
    protected abstract void ConfigureServices(WebApplicationBuilder builder);

    /// <summary>Runs the standard sequence and returns the built application, ready to have further endpoints
    /// mapped and to run.</summary>
    /// <exception cref="InvalidOperationException">The application was already built.</exception>
    public WebApplication Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("The application was already built; Build can be called only once.");
        }

        _built = true;

        Builder.LoadConfiguration(Parameters);

        Builder.Services.AddControllers();

        if (Options.UseInvalidModelStateEnvelope)
        {
            Builder.Services.AddInvalidModelStateEnvelope();
        }

        Builder.AddWebApiSwaggerWith(Parameters, Options.Swagger);

        Builder.Services.Configure<TraceActivityOptions>(trace =>
        {
            trace.LogClientIp = Options.TraceActivity.LogClientIp;
            trace.TagClientAddress = Options.TraceActivity.TagClientAddress;
        });

        ConfigureServices(Builder);

        var app = Builder.Build();

        app.UseStandardMiddlewares(Options.CorsPolicy);
        app.UseWebApiMiddlewares(Options.Middlewares);
        app.MapControllers();

        return app;
    }

    /// <summary>Builds the application with <see cref="Build"/> and runs it, blocking until it shuts down.</summary>
    public void Run() => Build().Run();

    /// <summary>Builds the application with <see cref="Build"/> and runs it until it shuts down.</summary>
    public Task RunAsync() => Build().RunAsync();
}
