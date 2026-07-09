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

namespace ArturRios.Util.WebApi.Api.Configuration;

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

    protected readonly WebApplicationBuilder Builder = WebApplication.CreateBuilder(args);
    protected readonly WebApiParameters Parameters = new(args);

    private SettingsProvider _settings = null!;
    protected WebApplication App = null!;

    public abstract void Build();

    public void Run() => App.Run();

    public void BuildAndRun()
    {
        Build();
        Run();
    }

    public void BuildApp() => App = Builder.Build();

    public abstract void ConfigureApp();

    public virtual void AddDependencies() { }
    public virtual void ConfigureCors() { }
    public virtual void ConfigureSecurity() { }
    public virtual void ConfigureWebApi() { }
    public virtual void StartServices() { }

    public void AddLogging() => Builder.Services.AddLogging();

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

    public void LoadConfiguration()
    {
        Builder.Services.AddSingleton(sp =>
            new ConfigurationLoader(Builder.Configuration, Builder.Environment.EnvironmentName,
                null, sp.GetRequiredService<ILogger<ConfigurationLoader>>()));

        using var provider = Builder.Services.BuildServiceProvider();
        var configurationLoader = provider.GetRequiredService<ConfigurationLoader>();

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
