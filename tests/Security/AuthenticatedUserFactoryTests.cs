using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Factories;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthenticatedUserFactoryTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static string CreateToken(AuthenticatedUser user)
    {
        var handler = new JwtHandler();
        var configuration = new JwtConfiguration(3600, "issuer", "audience", Secret, user.ToTokenClaims());

        return handler.CreateToken(configuration);
    }

    [Fact]
    public void FromToken_ShouldReconstructUser_FromIdAndRoleClaims()
    {
        var token = CreateToken(new AuthenticatedUser(42, 3));

        var user = AuthenticatedUserFactory.FromToken(token);

        Assert.NotNull(user);
        Assert.Equal(42, user.Id);
        Assert.Equal(3, user.Role);
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenTokenIsNotReadable()
    {
        var user = AuthenticatedUserFactory.FromToken("not-a-jwt");

        Assert.Null(user);
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenTokenIsEmpty()
    {
        var user = AuthenticatedUserFactory.FromToken(string.Empty);

        Assert.Null(user);
    }
}
