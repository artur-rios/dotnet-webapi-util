using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Mappers;
using ArturRios.Util.WebApi.Security.Providers;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security.Extensions;

[Trait("Category", "Unit")]
public class AuthenticationServiceCollectionExtensionsTests
{
    private sealed class FakeAuthenticationProvider : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => new AuthenticatedUser(id, 1);
        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    private sealed class StubMapper : IAuthenticatedUserMapper
    {
        public Dictionary<string, string> ToClaims(IAuthenticatedUser user) => new();
        public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims) => null;
    }

    [Fact]
    public void GivenTheCachingDecoratorIsRegistered_WhenResolvingTheProvider_ThenTheDecoratedOneComesBack()
    {
        var services = new ServiceCollection();

        services.AddCachedAuthenticationProvider<FakeAuthenticationProvider>(options =>
            options.Ttl = TimeSpan.FromSeconds(5));

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IAuthenticationProvider>();

        var id = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

        var user = Assert.IsType<CachedAuthenticationProvider>(resolved).GetAuthenticatedUserById(id);
        Assert.Equal(id, user!.Id);
    }

    [Fact]
    public void GivenNoSchemeIsEnabled_WhenAddingTokenAuthentication_ThenRegistrationFails()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddTokenAuthentication(o => { o.EnableJwt = false; o.EnableGoogle = false; }));
    }

    [Fact]
    public void GivenGoogleIsEnabledWithoutClientIds_WhenAddingTokenAuthentication_ThenRegistrationFails()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddTokenAuthentication(o => { o.EnableGoogle = true; }));
    }

    [Fact]
    public void GivenDefaultOptions_WhenAddingTokenAuthentication_ThenTheJwtValidatorIsRegistered()
    {
        // JwtTokenValidator needs JwtConfiguration/JwtHandler, which this test does not register, so we assert
        // on the ServiceDescriptor registrations/order rather than resolving instances (per task brief guidance).
        var services = new ServiceCollection();
        services.AddTokenAuthentication(_ => { });

        var validatorDescriptors = services.Where(d => d.ServiceType == typeof(ITokenValidator)).ToArray();
        Assert.Single(validatorDescriptors);
        Assert.Equal(typeof(JwtTokenValidator), validatorDescriptors[0].ImplementationType);
    }

    [Fact]
    public void GivenGoogleIsEnabled_WhenAddingTokenAuthentication_ThenBothValidatorsAreRegisteredWithTheJwtOneFirst()
    {
        var services = new ServiceCollection();
        services.AddTokenAuthentication(o => { o.EnableGoogle = true; o.GoogleClientIds.Add("cid"); });

        var validatorDescriptors = services.Where(d => d.ServiceType == typeof(ITokenValidator)).ToArray();
        Assert.Equal(2, validatorDescriptors.Length);
        Assert.Equal(typeof(JwtTokenValidator), validatorDescriptors[0].ImplementationType);
        Assert.Equal(typeof(GoogleTokenValidator), validatorDescriptors[1].ImplementationType);
    }

    [Fact]
    public void GivenNoMapperIsSpecified_WhenAddingTokenAuthentication_ThenTheDefaultMapperIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddTokenAuthentication(_ => { });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IAuthenticatedUserMapper));
        Assert.Equal(typeof(DefaultAuthenticatedUserMapper), descriptor.ImplementationType);
    }

    [Fact]
    public void GivenAMapperIsSpecified_WhenAddingTokenAuthentication_ThenThatMapperIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddTokenAuthentication<StubMapper>(_ => { });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IAuthenticatedUserMapper));
        Assert.Equal(typeof(StubMapper), descriptor.ImplementationType);
    }

    [Fact]
    public void GivenTokenAuthenticationIsAddedTwice_WhenInspected_ThenTheJwtValidatorIsRegisteredOnce()
    {
        var services = new ServiceCollection();

        services.AddTokenAuthentication(_ => { });
        services.AddTokenAuthentication(_ => { });

        Assert.Single(services, d => d.ServiceType == typeof(ITokenValidator));
    }

    [Fact]
    public void GivenANonPositiveCacheTtl_WhenAddingTheCachedProvider_ThenRegistrationFails()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddCachedAuthenticationProvider<NoUserProvider>(options => options.Ttl = TimeSpan.Zero));
    }

    private sealed class NoUserProvider : IAuthenticationProvider
    {
        public IAuthenticatedUser? GetAuthenticatedUserById(Guid id) => null;

        public IAuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    [Fact]
    public void GivenNoConfigureCallback_WhenAddingTheCachedProvider_ThenTheCachingDecoratorIsRegisteredWithAMemoryCache()
    {
        var services = new ServiceCollection();

        services.AddCachedAuthenticationProvider<FakeAuthenticationProvider>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        Assert.IsType<CachedAuthenticationProvider>(scope.ServiceProvider.GetRequiredService<IAuthenticationProvider>());
        Assert.IsType<FakeAuthenticationProvider>(scope.ServiceProvider.GetRequiredService<FakeAuthenticationProvider>());
        Assert.NotNull(scope.ServiceProvider.GetService<IMemoryCache>());
    }

    [Fact]
    public void GivenOnlyGoogleIsEnabled_WhenAddingTokenAuthentication_ThenOnlyTheGoogleValidatorAndItsVerifierAreRegistered()
    {
        var services = new ServiceCollection();

        services.AddTokenAuthentication(options =>
        {
            options.EnableJwt = false;
            options.EnableGoogle = true;
            options.GoogleClientIds.Add("client-id-1");
        });

        var validator = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITokenValidator));
        Assert.Equal(typeof(GoogleTokenValidator), validator.ImplementationType);
        Assert.Equal(typeof(GoogleTokenVerifier),
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IGoogleTokenVerifier)).ImplementationType);
    }

    [Fact]
    public void GivenAMapperIsAlreadyRegistered_WhenAddingTokenAuthentication_ThenThatMapperIsKept()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticatedUserMapper, StubMapper>();

        services.AddTokenAuthentication(_ => { });

        Assert.Equal(typeof(StubMapper),
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAuthenticatedUserMapper)).ImplementationType);
    }

    [Fact]
    public void GivenConfiguredOptions_WhenAddingTokenAuthentication_ThenThoseOptionsAreRegistered()
    {
        var services = new ServiceCollection();

        services.AddTokenAuthentication(options =>
        {
            options.Source = TokenSource.Cookie;
            options.CookieName = "session";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var registered = serviceProvider.GetRequiredService<AuthenticationOptions>();

        Assert.Equal(TokenSource.Cookie, registered.Source);
        Assert.Equal("session", registered.CookieName);
    }

    [Fact]
    public void GivenANullConfigureCallback_WhenAddingTokenAuthentication_ThenItThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddTokenAuthentication(null!));
    }
}
