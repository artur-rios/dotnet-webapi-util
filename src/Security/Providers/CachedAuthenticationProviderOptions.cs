namespace ArturRios.Util.WebApi.Security.Providers;

/// <summary>
/// Options for <see cref="CachedAuthenticationProvider"/>.
/// </summary>
public class CachedAuthenticationProviderOptions
{
    /// <summary>
    /// How long a resolved user stays cached before the underlying provider is queried again.
    /// Defaults to 60 seconds. Keep it short so role changes and revocations surface quickly.
    /// </summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// When <see langword="true"/>, "user not found" results are also cached for <see cref="Ttl"/>
    /// (negative caching), shielding the store from repeated lookups of unknown ids. Defaults to <see langword="false"/>.
    /// </summary>
    public bool CacheMisses { get; set; }

    /// <summary>
    /// The prefix used to build the cache key for each user id. Defaults to <c>auth:user:</c>.
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "auth:user:";
}
