using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Providers;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthenticationServiceCollectionExtensionsTests
{
    private sealed class FakeAuthenticationProvider : IAuthenticationProvider
    {
        public AuthenticatedUser? GetAuthenticatedUserById(int id) => new(id, 1);
        public AuthenticatedUser? GetAuthenticatedUserByEmail(string email) => null;
    }

    [Fact]
    public void AddCachedAuthenticationProvider_ResolvesDecoratedProvider()
    {
        var services = new ServiceCollection();

        services.AddCachedAuthenticationProvider<FakeAuthenticationProvider>(options =>
            options.Ttl = TimeSpan.FromSeconds(5));

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IAuthenticationProvider>();

        var user = Assert.IsType<CachedAuthenticationProvider>(resolved).GetAuthenticatedUserById(7);
        Assert.Equal(7, user!.Id);
    }

    [Fact]
    public void AddTokenAuthentication_Throws_WhenNoSchemeEnabled()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddTokenAuthentication(o => { o.EnableJwt = false; o.EnableGoogle = false; }));
    }

    [Fact]
    public void AddTokenAuthentication_Throws_WhenGoogleEnabledWithoutClientIds()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddTokenAuthentication(o => { o.EnableGoogle = true; }));
    }

    [Fact]
    public void AddTokenAuthentication_RegistersJwtValidator_ByDefault()
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
    public void AddTokenAuthentication_RegistersBothValidators_JwtFirst_WhenGoogleEnabled()
    {
        var services = new ServiceCollection();
        services.AddTokenAuthentication(o => { o.EnableGoogle = true; o.GoogleClientIds.Add("cid"); });

        var validatorDescriptors = services.Where(d => d.ServiceType == typeof(ITokenValidator)).ToArray();
        Assert.Equal(2, validatorDescriptors.Length);
        Assert.Equal(typeof(JwtTokenValidator), validatorDescriptors[0].ImplementationType);
        Assert.Equal(typeof(GoogleTokenValidator), validatorDescriptors[1].ImplementationType);
    }
}
