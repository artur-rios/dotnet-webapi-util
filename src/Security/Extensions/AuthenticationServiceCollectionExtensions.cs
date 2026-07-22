using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>
/// Dependency-injection helpers for registering authentication providers.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TProvider"/> as the underlying <see cref="IAuthenticationProvider"/> and
    /// exposes it through a <see cref="CachedAuthenticationProvider"/>, so repeated user lookups within the
    /// configured time-to-live are served from an <see cref="IMemoryCache"/> instead of the store.
    /// </summary>
    /// <typeparam name="TProvider">The concrete provider that performs the actual (e.g. database) lookup.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional callback to configure caching behavior (time-to-live, negative caching).</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddCachedAuthenticationProvider<TProvider>(
        this IServiceCollection services,
        Action<CachedAuthenticationProviderOptions>? configure = null)
        where TProvider : class, IAuthenticationProvider
    {
        services.AddMemoryCache();
        services.AddScoped<TProvider>();

        var options = new CachedAuthenticationProviderOptions();
        configure?.Invoke(options);

        services.AddScoped<IAuthenticationProvider>(serviceProvider =>
            new CachedAuthenticationProvider(
                serviceProvider.GetRequiredService<TProvider>(),
                serviceProvider.GetRequiredService<IMemoryCache>(),
                options));

        return services;
    }

    /// <summary>
    /// Registers the consolidated <see cref="AuthenticationOptions"/> and the enabled token validators used by
    /// <c>AuthenticationMiddleware</c>. Validators are registered app-JWT first, Google second, so the middleware
    /// tries them in that order. The app must separately register <c>JwtConfiguration</c> and <c>JwtHandler</c>
    /// (for JWT) and an <see cref="IAuthenticationProvider"/> (required for Google and for JWT <c>Revalidate</c> mode).
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Configures the options.</param>
    /// <exception cref="ArgumentException">No scheme enabled, or Google enabled without any client IDs.</exception>
    public static IServiceCollection AddTokenAuthentication(
        this IServiceCollection services, Action<AuthenticationOptions> configure)
    {
        var options = new AuthenticationOptions();
        configure(options);

        if (!options.EnableJwt && !options.EnableGoogle)
        {
            throw new ArgumentException("At least one authentication scheme (JWT or Google) must be enabled.");
        }

        if (options.EnableGoogle && options.GoogleClientIds.Count == 0)
        {
            throw new ArgumentException("EnableGoogle requires at least one entry in GoogleClientIds.");
        }

        services.AddSingleton(options);

        if (options.EnableJwt)
        {
            services.AddSingleton<ITokenValidator, JwtTokenValidator>();
        }

        if (options.EnableGoogle)
        {
            services.TryAddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();
            services.AddSingleton<ITokenValidator, GoogleTokenValidator>();
        }

        return services;
    }
}
