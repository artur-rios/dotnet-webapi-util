using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Factories;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthenticatedUserFactoryTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private static string CreateToken(AuthenticatedUser user) => CreateToken(user.ToTokenClaims());

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    [Fact]
    public void FromToken_ShouldReconstructUser_FromIdAndRoleClaims()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3));

        var user = AuthenticatedUserFactory.FromToken(token);

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenIdIsNotAGuid()
    {
        var token = CreateToken(new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, "42" }, { TokenClaimKeys.RoleId, "3" }
        });

        Assert.Null(AuthenticatedUserFactory.FromToken(token));
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenRoleIdIsNotNumeric()
    {
        var token = CreateToken(new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, UserId.ToString() }, { TokenClaimKeys.RoleId, "admin" }
        });

        Assert.Null(AuthenticatedUserFactory.FromToken(token));
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenTokenIsNotReadable()
    {
        Assert.Null(AuthenticatedUserFactory.FromToken("not-a-jwt"));
    }

    [Fact]
    public void FromToken_ShouldReturnNull_WhenTokenIsEmpty()
    {
        Assert.Null(AuthenticatedUserFactory.FromToken(string.Empty));
    }

    [Fact]
    public void IdFromToken_ShouldReturnId_WhenClaimIsAGuid()
    {
        var token = CreateToken(new AuthenticatedUser(UserId, 3));

        Assert.Equal(UserId, AuthenticatedUserFactory.IdFromToken(token));
    }

    [Fact]
    public void IdFromToken_ShouldReturnNull_WhenIdIsNotAGuid()
    {
        var token = CreateToken(new Dictionary<string, string> { { TokenClaimKeys.Id, "42" } });

        Assert.Null(AuthenticatedUserFactory.IdFromToken(token));
    }

    [Fact]
    public void IdFromToken_ShouldReturnNull_WhenTokenIsNotReadable()
    {
        Assert.Null(AuthenticatedUserFactory.IdFromToken("not-a-jwt"));
    }
}
