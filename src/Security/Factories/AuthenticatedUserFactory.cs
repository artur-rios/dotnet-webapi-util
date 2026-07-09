using System.IdentityModel.Tokens.Jwt;
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Factories;

/// <summary>
/// Reconstructs an <see cref="AuthenticatedUser"/> from the claims embedded in a JSON Web Token,
/// without hitting any data store. Callers must validate the token's signature before trusting the result.
/// </summary>
public static class AuthenticatedUserFactory
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>
    /// Builds an <see cref="AuthenticatedUser"/> from the token's <c>id</c> and <c>role</c> claims.
    /// </summary>
    /// <param name="token">The JWT to read. Its signature is not validated here.</param>
    /// <returns>
    /// The reconstructed user, or <see langword="null"/> if the token cannot be read or is missing a
    /// numeric <c>id</c> or <c>role</c> claim.
    /// </returns>
    public static AuthenticatedUser? FromToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Handler.CanReadToken(token))
        {
            return null;
        }

        var claims = Handler.ReadJwtToken(token).Claims.ToArray();

        var idClaim = claims.FirstOrDefault(claim => claim.Type == TokenClaimKeys.Id)?.Value;
        var roleClaim = claims.FirstOrDefault(claim => claim.Type == TokenClaimKeys.Role)?.Value;

        if (!int.TryParse(idClaim, out var id) || !int.TryParse(roleClaim, out var role))
        {
            return null;
        }

        return new AuthenticatedUser(id, role);
    }
}
