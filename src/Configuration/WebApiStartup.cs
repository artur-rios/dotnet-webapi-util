using ArturRios.Configuration.Enums;
using ArturRios.Configuration.Loaders;
using ArturRios.Configuration.Providers;
using ArturRios.Extensions;
using ArturRios.Output;
using ArturRios.Util.WebApi.Extensions;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>
/// Base class for bootstrapping an ASP.NET Core web API: builds the <see cref="WebApplicationBuilder"/> and
/// <see cref="WebApplication"/>, wires up configuration, middlewares and Swagger, and exposes hooks
/// (<see cref="Build"/>, <see cref="ConfigureApp"/>, <see cref="AddDependencies"/>, etc.) for derived classes
/// to customize the pipeline.
/// </summary>
/// <param name="args">The command-line arguments passed to the application entry point.</param>
public abstract class WebApiStartup(string[] args)
{
    /// <summary>The startup parameters parsed from the command-line arguments.</summary>
    protected readonly WebApiParameters Parameters = new(args);

    /// <summary>The builder used to configure services before the application is built. Its environment is
    /// <see cref="WebApiParameters.EnvironmentName"/> when one was supplied.</summary>
    protected readonly WebApplicationBuilder Builder = WebApplication.CreateBuilder(CreateOptions(args));

    /// <summary>The built application. Populated by <see cref="BuildApp"/>.</summary>
    protected WebApplication App = null!;

    /// <summary>Performs the full startup sequence (configuration, services, middlewares, etc.). Implemented by derived classes.</summary>
    public abstract void Build();

    /// <summary>Runs the built application, blocking until it shuts down.</summary>
    /// <exception cref="InvalidOperationException"><see cref="BuildApp"/> has not been called.</exception>
    public void Run()
    {
        if (App is null)
        {
            throw new InvalidOperationException($"Call {nameof(BuildApp)} before {nameof(Run)}.");
        }

        App.Run();
    }

    /// <summary>Calls <see cref="Build"/> followed by <see cref="Run"/>.</summary>
    public void BuildAndRun()
    {
        Build();
        Run();
    }

    /// <summary>Builds <see cref="App"/> from <see cref="Builder"/>.</summary>
    public void BuildApp() => App = Builder.Build();

    /// <summary>Configures the built application's request pipeline. Implemented by derived classes.</summary>
    public abstract void ConfigureApp();

    /// <summary>Registers application-specific dependencies. Override to add custom services.</summary>
    public virtual void AddDependencies() { }

    /// <summary>Configures CORS policies. Override to enable and customize CORS.</summary>
    public virtual void ConfigureCors() { }

    /// <summary>Configures authentication/authorization. Override to enable custom security.</summary>
    public virtual void ConfigureSecurity() { }

    /// <summary>Configures web API-specific services (controllers, filters, etc.). Override to customize.</summary>
    public virtual void ConfigureWebApi() { }

    /// <summary>Starts background or hosted services. Override to start custom services.</summary>
    public virtual void StartServices() { }

    /// <summary>Loads application settings and/or the environment file according to <see cref="Parameters"/>,
    /// and registers <see cref="SettingsProvider"/> (always, since <c>AuthenticationMiddleware</c> depends on it)
    /// and <see cref="EnvironmentProvider"/> (when the environment file is loaded) as services.</summary>
    public void LoadConfiguration()
    {
        Builder.Services.AddSingleton(sp =>
            new ConfigurationLoader(Builder.Configuration, Builder.Environment.EnvironmentName,
                null, sp.GetRequiredService<ILogger<ConfigurationLoader>>()));

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var configurationLoader = new ConfigurationLoader(
            Builder.Configuration, Builder.Environment.EnvironmentName, null,
            loggerFactory.CreateLogger<ConfigurationLoader>());

        SetSwaggerConfigFromParameters();

        Builder.Services.TryAddSingleton<SettingsProvider>();

        if (Parameters.UseAppSettings)
        {
            configurationLoader.LoadAppSettings();
        }

        if (Parameters.UseEnvFile)
        {
            configurationLoader.LoadEnvironment();

            Builder.Services.AddSingleton<EnvironmentProvider>();
        }
    }

    /// <summary>Registers each given middleware type on the application pipeline, in order.</summary>
    /// <param name="middlewares">The middleware types to register, in pipeline order.</param>
    /// <exception cref="ArgumentException">A type does not derive from <see cref="WebApiMiddleware"/>.</exception>
    public void AddMiddlewares(params Type[] middlewares) => App.UseWebApiMiddlewares(middlewares);

    /// <summary>Replaces ASP.NET Core's default invalid-model-state response with a 400 result carrying a
    /// <see cref="DataOutput{T}"/> whose errors list each invalid parameter and its message.</summary>
    public void AddCustomInvalidModelStateResponse() =>
        Builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .Select(e => $"Parameter: {e.Key} | Error: {e.Value?.Errors.First().ErrorMessage}").ToArray();

                var output = DataOutput<string>.New
                    .WithData(string.Empty)
                    .WithErrors(errors);

                return new BadRequestObjectResult(output);
            };
        });

    /// <summary>Enables the Swagger middleware (JSON endpoint and UI) when Swagger is allowed in the current
    /// environment (see <see cref="IsSwaggerAllowed"/>), and marks it enabled under
    /// <see cref="AppSettingsKeys.SwaggerEnabled"/> so <c>AuthenticationMiddleware</c> lets <c>/swagger</c> through.</summary>
    /// <param name="allowedEnvironments">Optional explicit list of environments in which Swagger should be served.</param>
    public void UseSwagger(EnvironmentType[]? allowedEnvironments = null)
    {
        if (!IsSwaggerAllowed(allowedEnvironments))
        {
            return;
        }

        MarkSwaggerEnabled();

        App.UseSwagger();
        App.UseSwaggerUI();
    }

    /// <summary>Registers the Swagger generator (<c>AddSwaggerGen</c>) when Swagger is allowed in the current
    /// environment (see <see cref="IsSwaggerAllowed"/>), optionally applying custom <see cref="SwaggerGenOptions"/>
    /// and/or JWT bearer security definitions.</summary>
    /// <param name="allowedEnvironments">Optional explicit list of environments in which Swagger docs should be generated.</param>
    /// <param name="swaggerGenOptions">Optional callback to further configure <see cref="SwaggerGenOptions"/>.</param>
    /// <param name="jwtAuthentication">Whether to add a JWT bearer security definition/requirement to the generated docs.</param>
    public void UseSwaggerGen(EnvironmentType[]? allowedEnvironments = null,
        Action<SwaggerGenOptions>? swaggerGenOptions = null, bool jwtAuthentication = false)
    {
        if (!IsSwaggerAllowed(allowedEnvironments))
        {
            return;
        }

        Builder.Services.AddSwaggerGen(options =>
        {
            swaggerGenOptions?.Invoke(options);

            if (jwtAuthentication)
            {
                AddJwtSecurity(options);
            }
        });
    }

    /// <summary>Whether Swagger is allowed in the current environment. Never when the <c>EnableSwaggerDocs:false</c>
    /// argument was supplied; otherwise when the environment is in <paramref name="allowedEnvironments"/>, if
    /// non-empty, or else in <see cref="WebApiParameters.GetSwaggerEnvironments"/> (the <c>SwaggerEnvironments</c>
    /// argument, falling back to <c>Development</c> and <c>Local</c>).</summary>
    /// <param name="allowedEnvironments">Optional explicit list of environments in which Swagger is allowed.</param>
    protected bool IsSwaggerAllowed(EnvironmentType[]? allowedEnvironments = null)
    {
        if (!Parameters.EnableSwaggerDocs)
        {
            return false;
        }

        var currentEnv = Builder.Environment.EnvironmentName;

        var environments = allowedEnvironments.IsNotEmpty()
            ? allowedEnvironments!.Select(env => env.ToString())
            : Parameters.GetSwaggerEnvironments();

        return environments.Contains(currentEnv, StringComparer.OrdinalIgnoreCase);
    }

    private static WebApplicationOptions CreateOptions(string[] args)
    {
        var environmentName = new WebApiParameters(args).EnvironmentName;

        return new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = string.IsNullOrWhiteSpace(environmentName) ? null : environmentName
        };
    }

    private static void AddJwtSecurity(SwaggerGenOptions options)
    {
        var jwtSecurityScheme = new OpenApiSecurityScheme
        {
            BearerFormat = "JWT",
            Name = "JWT Authentication",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            Description =
                "After getting a token from the Authentication route, put **_ONLY_** your JWT Bearer token on textbox below"
        };

        var jwtRequirement = new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme), [] }
        };

        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
        options.AddSecurityRequirement(_ => jwtRequirement);
    }

    private void SetSwaggerConfigFromParameters()
    {
        var currentEnv = Builder.Environment.EnvironmentName;

        if (!Parameters.EnableSwaggerDocs ||
            !Parameters.SwaggerEnvironments.Contains(currentEnv, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        MarkSwaggerEnabled();
    }

    private void MarkSwaggerEnabled() =>
        Builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { [AppSettingsKeys.SwaggerEnabled] = "true" });
}
