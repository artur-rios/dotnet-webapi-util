using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Providers;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.Extensions.Caching.Memory;

namespace ArturRios.Util.WebApi.Tests.Security;

public class CachedAuthenticationProviderTests
{
    private sealed class CountingAuthenticationProvider(Func<int, AuthenticatedUser?> resolve) : IAuthenticationProvider
    {
        public int CallCount { get; private set; }

        public AuthenticatedUser? GetAuthenticatedUserById(int id)
        {
            CallCount++;

            return resolve(id);
        }
    }

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    [Fact]
    public void SecondLookupWithinTtl_IsServedFromCache()
    {
        var inner = new CountingAuthenticationProvider(id => new AuthenticatedUser(id, 1));
        var provider = new CachedAuthenticationProvider(inner, NewCache());

        var first = provider.GetAuthenticatedUserById(42);
        var second = provider.GetAuthenticatedUserById(42);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(first, second);
        Assert.Equal(42, second!.Id);
    }

    [Fact]
    public void DifferentIds_AreCachedIndependently()
    {
        var inner = new CountingAuthenticationProvider(id => new AuthenticatedUser(id, 1));
        var provider = new CachedAuthenticationProvider(inner, NewCache());

        provider.GetAuthenticatedUserById(1);
        provider.GetAuthenticatedUserById(2);
        provider.GetAuthenticatedUserById(1);

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void Misses_AreNotCached_ByDefault()
    {
        var inner = new CountingAuthenticationProvider(_ => null);
        var provider = new CachedAuthenticationProvider(inner, NewCache());

        provider.GetAuthenticatedUserById(42);
        provider.GetAuthenticatedUserById(42);

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void Misses_AreCached_WhenNegativeCachingEnabled()
    {
        var inner = new CountingAuthenticationProvider(_ => null);
        var options = new CachedAuthenticationProviderOptions { CacheMisses = true };
        var provider = new CachedAuthenticationProvider(inner, NewCache(), options);

        provider.GetAuthenticatedUserById(42);
        provider.GetAuthenticatedUserById(42);

        Assert.Equal(1, inner.CallCount);
    }
}
