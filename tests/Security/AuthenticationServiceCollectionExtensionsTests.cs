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
}
