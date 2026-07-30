using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    /// The reconstructed user, or <see langword="null"/> if the token cannot be read, has a blank
    /// <c>id</c> claim, or is missing a numeric <c>role</c> claim.
    /// </returns>
    public static AuthenticatedUser? FromToken(string token)
    {
        var claims = ReadClaims(token);

        if (claims is null)
        {
            return null;
        }

        var id = ClaimValue(claims, TokenClaimKeys.Id);
        var roleClaim = ClaimValue(claims, TokenClaimKeys.Role);

        if (string.IsNullOrWhiteSpace(id) || !int.TryParse(roleClaim, out var role))
        {
            return null;
        }

        return new AuthenticatedUser(id, role);
    }

    /// <summary>
    /// Reads just the user id from the token's <c>id</c> claim, for callers that resolve the rest of the
    /// user from a data store.
    /// </summary>
    /// <param name="token">The JWT to read. Its signature is not validated here.</param>
    /// <returns>The user id, or <see langword="null"/> if the token cannot be read or its <c>id</c> claim is blank.</returns>
    public static string? IdFromToken(string token)
    {
        var claims = ReadClaims(token);
        var id = claims is null ? null : ClaimValue(claims, TokenClaimKeys.Id);

        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    private static Claim[]? ReadClaims(string token) =>
        string.IsNullOrWhiteSpace(token) || !Handler.CanReadToken(token)
            ? null
            : Handler.ReadJwtToken(token).Claims.ToArray();

    private static string? ClaimValue(Claim[] claims, string key) =>
        claims.FirstOrDefault(claim => claim.Type == key)?.Value;
}
