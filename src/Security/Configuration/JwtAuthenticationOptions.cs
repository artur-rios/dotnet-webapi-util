using ArturRios.Util.WebApi.Security.Enums;

namespace ArturRios.Util.WebApi.Security.Configuration;

/// <summary>
/// Options that control how <c>JwtMiddleware</c> authenticates requests.
/// </summary>
public class JwtAuthenticationOptions
{
    /// <summary>
    /// How the authenticated user is resolved once the token's signature is validated.
    /// Defaults to <see cref="JwtValidationMode.ClaimsOnly"/> (stateless, no data-store lookup).
    /// </summary>
    public JwtValidationMode ValidationMode { get; set; } = JwtValidationMode.ClaimsOnly;
}
