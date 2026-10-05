using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ArturRios.Util.WebApi.Security.Providers;

/// <summary>
/// An <see cref="IAuthenticationProvider"/> decorator that caches resolved users in an
/// <see cref="IMemoryCache"/>, so repeated lookups of the same user within the configured
/// time-to-live are served from memory instead of the underlying store.
/// </summary>
public class CachedAuthenticationProvider(
    IAuthenticationProvider inner,
    IMemoryCache cache,
    CachedAuthenticationProviderOptions? options = null) : IAuthenticationProvider
{
    private readonly CachedAuthenticationProviderOptions _options = options ?? new CachedAuthenticationProviderOptions();

    /// <inheritdoc />
    public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) =>
        GetOrLoad($"{_options.CacheKeyPrefix}{id}", () => inner.GetAuthenticatedUserById(id));

    /// <inheritdoc />
    public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) =>
        GetOrLoad($"{_options.EmailCacheKeyPrefix}{email}", () => inner.GetAuthenticatedUserByEmail(email));

    private IAuthenticatedUser? GetOrLoad(string key, Func<IAuthenticatedUser?> load)
    {
        if (cache.TryGetValue(key, out IAuthenticatedUser? cachedUser))
        {
            return cachedUser;
        }

        var user = load();

        if (user is not null || _options.CacheMisses)
        {
            cache.Set(key, user, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.Ttl });
        }

        return user;
    }
}
