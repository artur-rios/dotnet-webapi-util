using System.IdentityModel.Tokens.Jwt;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>
/// Reads a JSON Web Token's claims into a dictionary, without validating its signature. Callers must
/// validate the signature before trusting the result.
/// </summary>
public static class TokenClaimsReader
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>Reads the token's claims. A repeated claim key keeps its first occurrence.</summary>
    /// <param name="token">The JWT to read. Its signature is not validated here.</param>
    /// <returns>The token's claims, or <see langword="null"/> if the token is blank or cannot be read.</returns>
    public static IReadOnlyDictionary<string, string>? Read(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Handler.CanReadToken(token))
        {
            return null;
        }

        var claims = new Dictionary<string, string>();

        foreach (var claim in Handler.ReadJwtToken(token).Claims)
        {
            claims.TryAdd(claim.Type, claim.Value);
        }

        return claims;
    }
}
