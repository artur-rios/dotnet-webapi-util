using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Providers;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.Extensions.Caching.Memory;

namespace ArturRios.Util.WebApi.Tests.Security;

public class CachedAuthenticationProviderTests
{
    private sealed class CountingProvider(AuthenticatedUser? byId = null, AuthenticatedUser? byEmail = null) : IAuthenticationProvider
    {
        public int IdCallCount { get; private set; }
        public int EmailCallCount { get; private set; }

        public AuthenticatedUser? GetAuthenticatedUserById(int id)
        {
            IdCallCount++;
            return byId;
        }

        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email)
        {
            EmailCallCount++;
            return byEmail;
        }
    }

    private sealed class CountingAuthenticationProvider(Func<int, AuthenticatedUser?> resolve) : IAuthenticationProvider
    {
        public int CallCount { get; private set; }

        public AuthenticatedUser? GetAuthenticatedUserById(int id)
        {
            CallCount++;

            return resolve(id);
        }

        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
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

    [Fact]
    public void GetAuthenticatedUserByEmail_CachesPositiveResult()
    {
        var inner = new CountingProvider(byEmail: new AuthenticatedUser(5, 1));
        var cache = NewCache();
        var provider = new CachedAuthenticationProvider(inner, cache);

        var first = provider.GetAuthenticatedUserByEmail("a@b.com");
        var second = provider.GetAuthenticatedUserByEmail("a@b.com");

        Assert.Equal(5, first!.Id);
        Assert.Equal(5, second!.Id);
        Assert.Equal(1, inner.EmailCallCount);
    }

    [Fact]
    public void GetAuthenticatedUserByEmail_CachesMiss_WhenEnabled()
    {
        var inner = new CountingProvider(byEmail: null);
        var cache = NewCache();
        var provider = new CachedAuthenticationProvider(inner, cache, new CachedAuthenticationProviderOptions { CacheMisses = true });

        provider.GetAuthenticatedUserByEmail("missing@b.com");
        provider.GetAuthenticatedUserByEmail("missing@b.com");

        Assert.Equal(1, inner.EmailCallCount);
    }

    [Fact]
    public void EmailAndIdCaches_AreIndependent()
    {
        var inner = new CountingProvider(byId: new AuthenticatedUser(9, 1), byEmail: new AuthenticatedUser(5, 1));
        var cache = NewCache();
        var provider = new CachedAuthenticationProvider(inner, cache);

        provider.GetAuthenticatedUserById(9);
        provider.GetAuthenticatedUserByEmail("a@b.com");

        Assert.Equal(1, inner.IdCallCount);
        Assert.Equal(1, inner.EmailCallCount);
    }
}
