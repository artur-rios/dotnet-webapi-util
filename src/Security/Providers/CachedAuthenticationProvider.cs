using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
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
    public AuthenticatedUser? GetAuthenticatedUserById(int id)
    {
        var key = $"{_options.CacheKeyPrefix}{id}";

        if (cache.TryGetValue(key, out AuthenticatedUser? cachedUser))
        {
            return cachedUser;
        }

        var user = inner.GetAuthenticatedUserById(id);

        if (user is not null || _options.CacheMisses)
        {
            cache.Set(key, user, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.Ttl });
        }

        return user;
    }

    /// <inheritdoc />
    public AuthenticatedUser? GetAuthenticatedUserByEmail(string email)
    {
        var key = $"{_options.EmailCacheKeyPrefix}{email}";

        if (cache.TryGetValue(key, out AuthenticatedUser? cachedUser))
        {
            return cachedUser;
        }

        var user = inner.GetAuthenticatedUserByEmail(email);

        if (user is not null || _options.CacheMisses)
        {
            cache.Set(key, user, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.Ttl });
        }

        return user;
    }
}
