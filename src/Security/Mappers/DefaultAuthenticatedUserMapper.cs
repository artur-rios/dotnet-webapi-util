using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Mappers;

/// <summary>
/// The mapper used when an app registers none of its own: it maps <see cref="IAuthenticatedUser.Id"/> and
/// <see cref="IAuthenticatedUser.RoleId"/> through <see cref="TokenClaimKeys"/> and produces an
/// <see cref="AuthenticatedUser"/>. Also the reference implementation for writing your own.
/// </summary>
public class DefaultAuthenticatedUserMapper : IAuthenticatedUserMapper
{
    /// <inheritdoc />
    public Dictionary<string, string> ToClaims(IAuthenticatedUser user) =>
        new()
        {
            { TokenClaimKeys.Id, user.Id.ToString() },
            { TokenClaimKeys.RoleId, user.RoleId.ToString() }
        };

    /// <inheritdoc />
    public IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims)
    {
        if (!claims.TryGetValue(TokenClaimKeys.Id, out var idClaim) || !Guid.TryParse(idClaim, out var id))
        {
            return null;
        }

        if (!claims.TryGetValue(TokenClaimKeys.RoleId, out var roleClaim) || !int.TryParse(roleClaim, out var roleId))
        {
            return null;
        }

        return new AuthenticatedUser(id, roleId);
    }

    /// <inheritdoc />
    public Guid? IdFromClaims(IReadOnlyDictionary<string, string> claims) => FromClaims(claims)?.Id;
}
