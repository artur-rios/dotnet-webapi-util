using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Mappers;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Security;

public class DefaultAuthenticatedUserMapperTests
{
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private static readonly IAuthenticatedUserMapper Mapper = new DefaultAuthenticatedUserMapper();

    [Fact]
    public void ToClaims_WritesIdAndRoleId()
    {
        var claims = Mapper.ToClaims(new AuthenticatedUser(UserId, 3));

        Assert.Equal(UserId.ToString(), claims[TokenClaimKeys.Id]);
        Assert.Equal("3", claims[TokenClaimKeys.RoleId]);
    }

    [Fact]
    public void FromClaims_ReconstructsUser_FromClaimsWrittenByToClaims()
    {
        var user = Mapper.FromClaims(Mapper.ToClaims(new AuthenticatedUser(UserId, 3)));

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenIdIsNotAGuid()
    {
        var claims = new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, "not-a-guid" }, { TokenClaimKeys.RoleId, "3" }
        };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenRoleIdIsNotNumeric()
    {
        var claims = new Dictionary<string, string>
        {
            { TokenClaimKeys.Id, UserId.ToString() }, { TokenClaimKeys.RoleId, "admin" }
        };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenIdClaimIsMissing()
    {
        var claims = new Dictionary<string, string> { { TokenClaimKeys.RoleId, "3" } };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void FromClaims_ReturnsNull_WhenRoleIdClaimIsMissing()
    {
        var claims = new Dictionary<string, string> { { TokenClaimKeys.Id, UserId.ToString() } };

        Assert.Null(Mapper.FromClaims(claims));
    }

    [Fact]
    public void IdFromClaims_ReturnsId_FromClaimsWrittenByToClaims()
    {
        var id = Mapper.IdFromClaims(Mapper.ToClaims(new AuthenticatedUser(UserId, 3)));

        Assert.Equal(UserId, id);
    }

    [Fact]
    public void IdFromClaims_ReturnsNull_WhenClaimsCannotProduceAUser()
    {
        Assert.Null(Mapper.IdFromClaims(new Dictionary<string, string>()));
    }
}
