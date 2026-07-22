namespace ArturRios.Util.WebApi.Security.Enums;

/// <summary>
/// Controls how <c>JwtTokenValidator</c> resolves the authenticated user for a validated app JWT.
/// </summary>
public enum JwtValidationMode
{
    /// <summary>
    /// Reconstruct the authenticated user from the token's claims only. No data store is queried,
    /// so role changes and revocations take effect only once the token expires. This is the default.
    /// </summary>
    ClaimsOnly,

    /// <summary>
    /// Re-fetch the authenticated user from <c>IAuthenticationProvider</c> on every request. Guarantees
    /// freshness and lets deleted users be rejected immediately, at the cost of a lookup per request.
    /// </summary>
    Revalidate
}
