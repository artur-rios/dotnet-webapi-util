using ArturRios.Configuration.Enums;
using ArturRios.Configuration.Loaders;
using ArturRios.Configuration.Providers;
using ArturRios.Extensions;
using ArturRios.Logging;
using ArturRios.Logging.Adapter;
using ArturRios.Logging.Configuration;
using ArturRios.Logging.Interfaces;
using ArturRios.Output;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>
/// Base class for bootstrapping an ASP.NET Core web API: builds the <see cref="WebApplicationBuilder"/> and
/// <see cref="WebApplication"/>, wires up configuration, logging, middlewares and Swagger, and exposes hooks
/// (<see cref="Build"/>, <see cref="ConfigureApp"/>, <see cref="AddDependencies"/>, etc.) for derived classes
/// to customize the pipeline.
/// </summary>
/// <param name="args">The command-line arguments passed to the application entry point.</param>
public abstract class WebApiStartup(string[] args)
{
    private readonly Action<SwaggerGenOptions> _swaggerGenJwtAuthentication = setup =>
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

        setup.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
        setup.AddSecurityRequirement(_ => jwtRequirement);
    };

    /// <summary>The builder used to configure services before the application is built.</summary>
    protected readonly WebApplicationBuilder Builder = WebApplication.CreateBuilder(args);

    /// <summary>The startup parameters parsed from the command-line arguments.</summary>
    protected readonly WebApiParameters Parameters = new(args);

    private SettingsProvider _settings = null!;

    /// <summary>The built application. Populated by <see cref="BuildApp"/>.</summary>
    protected WebApplication App = null!;

    /// <summary>Performs the full startup sequence (configuration, services, middlewares, etc.). Implemented by derived classes.</summary>
    public abstract void Build();

    /// <summary>Runs the built application, blocking until it shuts down.</summary>
    public void Run() => App.Run();

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

    /// <summary>Adds the default ASP.NET Core logging services.</summary>
    public void AddLogging() => Builder.Services.AddLogging();

    /// <summary>Replaces the default logging providers with the custom logger built from <paramref name="loggerConfigurations"/>.</summary>
    /// <param name="loggerConfigurations">The logger configurations to apply.</param>
    public void AddCustomLogging(List<LoggerConfiguration> loggerConfigurations)
    {
        Builder.Services.AddScoped<IStateLogger>(_ => new StateLogger(loggerConfigurations));

        Builder.Services.AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddCustomLogger();
            lb.SetMinimumLevel(LogLevel.Trace);
        });
    }

    /// <summary>Loads application settings and/or the environment file according to <see cref="Parameters"/>,
    /// and registers the resulting <see cref="SettingsProvider"/>/<see cref="EnvironmentProvider"/> as services.</summary>
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

        _settings = new SettingsProvider(Builder.Configuration);

        if (Parameters.UseAppSettings)
        {
            configurationLoader.LoadAppSettings();

            Builder.Services.AddSingleton<SettingsProvider>();
        }

        if (Parameters.UseEnvFile)
        {
            configurationLoader.LoadEnvironment();

            Builder.Services.AddSingleton<EnvironmentProvider>();
        }
    }

    /// <summary>Registers each given middleware type on the application pipeline, skipping any type that does
    /// not derive from <see cref="WebApiMiddleware"/>.</summary>
    /// <param name="middlewares">The middleware types to register, in pipeline order.</param>
    public void AddMiddlewares(Type[] middlewares)
    {
        foreach (var middleware in middlewares)
        {
            if (middleware.IsSubclassOf(typeof(WebApiMiddleware)))
            {
                App.UseMiddleware(middleware);
            }
        }
    }

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

    /// <summary>Enables the Swagger middleware (JSON endpoint and UI) when the current environment is allowed,
    /// as determined by <paramref name="allowedEnvironments"/>, <see cref="WebApiParameters.GetSwaggerEnvironments"/>,
    /// or <see cref="AppSettingsKeys.SwaggerEnabled"/>, in that order of precedence.</summary>
    /// <param name="allowedEnvironments">Optional explicit list of environments in which Swagger should be served.</param>
    public void UseSwagger(EnvironmentType[]? allowedEnvironments = null)
    {
        bool useSwagger;
        var currentEnv = Builder.Environment.EnvironmentName;
        var swaggerEnvs = Parameters.GetSwaggerEnvironments();

        if (allowedEnvironments.IsNotEmpty())
        {
            useSwagger = allowedEnvironments!.Any(env =>
                env.ToString().Equals(currentEnv, StringComparison.OrdinalIgnoreCase));
        }
        else if (swaggerEnvs.IsNotEmpty())
        {
            useSwagger = swaggerEnvs.Contains(currentEnv, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            useSwagger = _settings.GetBool(AppSettingsKeys.SwaggerEnabled) ?? false;
        }

        if (!useSwagger)
        {
            return;
        }

        App.UseSwagger();
        App.UseSwaggerUI();
    }

    /// <summary>Registers the Swagger generator (<c>AddSwaggerGen</c>) when the current environment is allowed,
    /// optionally applying custom <see cref="SwaggerGenOptions"/> and/or JWT bearer security definitions.</summary>
    /// <param name="allowedEnvironments">Optional explicit list of environments in which Swagger docs should be generated.</param>
    /// <param name="swaggerGenOptions">Optional callback to further configure <see cref="SwaggerGenOptions"/>.</param>
    /// <param name="jwtAuthentication">Whether to add a JWT bearer security definition/requirement to the generated docs.</param>
    public void UseSwaggerGen(EnvironmentType[]? allowedEnvironments = null,
        Action<SwaggerGenOptions>? swaggerGenOptions = null, bool jwtAuthentication = false)
    {
        var useSwaggerDocs = false;
        var currentEnv = Builder.Environment.EnvironmentName;
        var swaggerEnvs = Parameters.GetSwaggerEnvironments();

        if (allowedEnvironments.IsNotEmpty())
        {
            useSwaggerDocs = allowedEnvironments!.Any(env =>
                env.ToString().Equals(currentEnv, StringComparison.OrdinalIgnoreCase));
        }
        else if (swaggerEnvs.IsNotEmpty())
        {
            useSwaggerDocs = swaggerEnvs.Contains(currentEnv, StringComparer.OrdinalIgnoreCase);
        }

        if (!useSwaggerDocs)
        {
            return;
        }

        Builder.Services.AddSwaggerGen(options =>
        {
            swaggerGenOptions?.Invoke(options);

            if (jwtAuthentication)
            {
                _swaggerGenJwtAuthentication.Invoke(options);
            }
        });
    }

    private void SetSwaggerConfigFromParameters()
    {
        var currentEnv = Builder.Environment.EnvironmentName;

        if (!Parameters.SwaggerEnvironments.Contains(currentEnv, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var configValues = new Dictionary<string, string?> { [AppSettingsKeys.SwaggerEnabled] = "true" };

        Builder.Configuration.AddInMemoryCollection(configValues);
    }
}
