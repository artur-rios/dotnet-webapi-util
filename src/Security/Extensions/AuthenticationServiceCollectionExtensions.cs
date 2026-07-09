using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

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
}
