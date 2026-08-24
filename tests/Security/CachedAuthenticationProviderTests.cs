using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Providers;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.Extensions.Caching.Memory;

namespace ArturRios.Util.WebApi.Tests.Security;

[Trait("Category", "Unit")]
public class CachedAuthenticationProviderTests
{
    private static readonly Guid FirstId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");
    private static readonly Guid SecondId = Guid.Parse("7b644d0a-9f11-4c5d-8e6a-4b903f2a9c1e");

    private sealed class CountingProvider(IAuthenticatedUser? byId = null, IAuthenticatedUser? byEmail = null) : IAuthenticationProvider
    {
        public int IdCallCount { get; private set; }
        public int EmailCallCount { get; private set; }

        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
        {
            IdCallCount++;
            return byId;
        }

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email)
        {
            EmailCallCount++;
            return byEmail;
        }
    }

    private sealed class CountingAuthenticationProvider(Func<Guid, IAuthenticatedUser?> resolve) : IAuthenticationProvider
    {
        public int CallCount { get; private set; }

        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id)
        {
            CallCount++;

            return resolve(id);
        }

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    [Fact]
    public void GivenALookupWithinTheTimeToLive_WhenRepeated_ThenItIsServedFromTheCache()
    {
        var inner = new CountingAuthenticationProvider(id => new AuthenticatedUser(id, 1));
        var provider = new CachedAuthenticationProvider(inner, NewCache());

        var first = provider.GetAuthenticatedUserById(FirstId);
        var second = provider.GetAuthenticatedUserById(FirstId);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(first, second);
        Assert.Equal(FirstId, second!.Id);
    }

    [Fact]
    public void GivenTwoDifferentIds_WhenBothAreLookedUp_ThenEachIsCachedIndependently()
    {
        var inner = new CountingAuthenticationProvider(id => new AuthenticatedUser(id, 1));
        var provider = new CachedAuthenticationProvider(inner, NewCache());

        provider.GetAuthenticatedUserById(FirstId);
        provider.GetAuthenticatedUserById(SecondId);
        provider.GetAuthenticatedUserById(FirstId);

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void GivenDefaultOptions_WhenALookupMisses_ThenTheMissIsNotCached()
    {
        var inner = new CountingAuthenticationProvider(_ => null);
        var provider = new CachedAuthenticationProvider(inner, NewCache());

        provider.GetAuthenticatedUserById(FirstId);
        provider.GetAuthenticatedUserById(FirstId);

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void GivenNegativeCachingIsEnabled_WhenALookupMisses_ThenTheMissIsCached()
    {
        var inner = new CountingAuthenticationProvider(_ => null);
        var options = new CachedAuthenticationProviderOptions { CacheMisses = true };
        var provider = new CachedAuthenticationProvider(inner, NewCache(), options);

        provider.GetAuthenticatedUserById(FirstId);
        provider.GetAuthenticatedUserById(FirstId);

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void GivenALookupByEmailThatFindsAUser_WhenRepeated_ThenItIsServedFromTheCache()
    {
        var inner = new CountingProvider(byEmail: new AuthenticatedUser(SecondId, 1));
        var cache = NewCache();
        var provider = new CachedAuthenticationProvider(inner, cache);

        var first = provider.GetAuthenticatedUserByEmail("a@b.com");
        var second = provider.GetAuthenticatedUserByEmail("a@b.com");

        Assert.Equal(SecondId, first!.Id);
        Assert.Equal(SecondId, second!.Id);
        Assert.Equal(1, inner.EmailCallCount);
    }

    [Fact]
    public void GivenNegativeCachingIsEnabled_WhenALookupByEmailMisses_ThenTheMissIsCached()
    {
        var inner = new CountingProvider(byEmail: null);
        var cache = NewCache();
        var provider = new CachedAuthenticationProvider(inner, cache, new CachedAuthenticationProviderOptions { CacheMisses = true });

        provider.GetAuthenticatedUserByEmail("missing@b.com");
        provider.GetAuthenticatedUserByEmail("missing@b.com");

        Assert.Equal(1, inner.EmailCallCount);
    }

    [Fact]
    public void GivenTheSameUser_WhenLookedUpByIdAndByEmail_ThenTheTwoCachesStayIndependent()
    {
        var inner = new CountingProvider(byId: new AuthenticatedUser(FirstId, 1), byEmail: new AuthenticatedUser(SecondId, 1));
        var cache = NewCache();
        var provider = new CachedAuthenticationProvider(inner, cache);

        provider.GetAuthenticatedUserById(FirstId);
        provider.GetAuthenticatedUserByEmail("a@b.com");

        Assert.Equal(1, inner.IdCallCount);
        Assert.Equal(1, inner.EmailCallCount);
    }
}
