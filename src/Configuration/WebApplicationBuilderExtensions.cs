using ArturRios.Configuration.Loaders;
using ArturRios.Configuration.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Builder-side steps of the standard web API setup. <see cref="WebApiStartup"/> runs them in order, and each
/// one can be used on its own with a plain <see cref="WebApplicationBuilder"/>.</summary>
public static class WebApplicationBuilderExtensions
{
    /// <param name="builder">The application builder.</param>
    extension(WebApplicationBuilder builder)
    {
        /// <summary>Loads <c>appsettings</c> and/or the environment file according to <paramref name="parameters"/>, and
        /// registers <paramref name="parameters"/>, the <see cref="ConfigurationLoader"/>, <see cref="SettingsProvider"/>
        /// (always, since <c>AuthenticationMiddleware</c> depends on it) and <see cref="EnvironmentProvider"/> (when the
        /// environment file is loaded) as services.</summary>
        /// <param name="parameters">The startup parameters deciding which sources are loaded.</param>
        /// <returns>The same <paramref name="builder"/> instance, for chaining.</returns>
        public WebApplicationBuilder LoadConfiguration(WebApiParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            var environmentName = builder.Environment.EnvironmentName;

            builder.Services.TryAddSingleton(parameters);
            builder.Services.TryAddSingleton(sp => new ConfigurationLoader(builder.Configuration, environmentName, null,
                sp.GetRequiredService<ILogger<ConfigurationLoader>>()));
            builder.Services.TryAddSingleton<SettingsProvider>();

            using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());

            var configurationLoader = new ConfigurationLoader(builder.Configuration, environmentName, null,
                loggerFactory.CreateLogger<ConfigurationLoader>());

            if (parameters.UseAppSettings)
            {
                configurationLoader.LoadAppSettings();
            }

            if (parameters.UseEnvFile)
            {
                configurationLoader.LoadEnvironment();

                builder.Services.TryAddSingleton<EnvironmentProvider>();
            }

            return builder;
        }

        /// <summary>Registers the Swagger generator when Swagger is allowed in the current environment (see
        /// <see cref="WebApiParameters.IsSwaggerAllowed"/>), and marks it enabled under
        /// <see cref="AppSettingsKeys.SwaggerEnabled"/> so <c>AuthenticationMiddleware</c> lets <c>/swagger</c> through.
        /// Pair it with <see cref="ApplicationBuilderExtensions.UseWebApiSwagger"/>, which serves whatever was registered here.</summary>
        /// <param name="parameters">The startup parameters gating Swagger by environment.</param>
        /// <param name="configure">Optional callback to configure the allowed environments, JWT security and generator.</param>
        /// <returns>The same <paramref name="builder"/> instance, for chaining.</returns>
        public WebApplicationBuilder AddWebApiSwagger(WebApiParameters parameters, Action<WebApiSwaggerOptions>? configure = null)
        {
            var options = new WebApiSwaggerOptions();
            configure?.Invoke(options);

            return builder.AddWebApiSwaggerWith(parameters, options);
        }

        internal WebApplicationBuilder AddWebApiSwaggerWith(WebApiParameters parameters, WebApiSwaggerOptions options)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            if (!parameters.IsSwaggerAllowed(builder.Environment.EnvironmentName, options.AllowedEnvironments))
            {
                return builder;
            }

            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [AppSettingsKeys.SwaggerEnabled] = "true" });

            builder.Services.AddSwaggerGen(generator =>
            {
                options.ConfigureGenerator?.Invoke(generator);

                if (options.JwtAuthentication)
                {
                    AddJwtSecurity(generator);
                }
            });

            return builder;
        }
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

        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);

        // The reference must be bound to the document being generated: one without a host document has no target,
        // and Microsoft.OpenApi then writes the requirement as an empty object ({}), which tells Swagger UI the
        // operations need no credentials, so the token entered under "Authorize" was never sent.
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document), [] }
        });
    }
}
