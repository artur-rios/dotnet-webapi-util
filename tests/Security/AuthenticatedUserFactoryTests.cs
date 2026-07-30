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
        var token = CreateToken(new AuthenticatedUser("42", 3));

        var user = AuthenticatedUserFactory.FromToken(token);

        Assert.NotNull(user);
        Assert.Equal("42", user.Id);
        Assert.Equal(3, user.Role);
    }

    [Fact]
    public void FromToken_ShouldReconstructUser_WhenIdIsNotNumeric()
    {
        var token = CreateToken(new AuthenticatedUser("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90", 3));

        var user = AuthenticatedUserFactory.FromToken(token);

        Assert.NotNull(user);
        Assert.Equal("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90", user.Id);
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenIdClaimIsBlank()
    {
        var token = CreateToken(new AuthenticatedUser(" ", 3));

        var user = AuthenticatedUserFactory.FromToken(token);

        Assert.Null(user);
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

    [Fact]
    public void IdFromToken_ShouldReturnIdClaim_AsWritten()
    {
        var token = CreateToken(new AuthenticatedUser("user-42", 3));

        Assert.Equal("user-42", AuthenticatedUserFactory.IdFromToken(token));
    }

    [Fact]
    public void IdFromToken_ShouldReturnNull_WhenTokenIsNotReadable()
    {
        Assert.Null(AuthenticatedUserFactory.IdFromToken("not-a-jwt"));
    }
}
