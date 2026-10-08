using ArturRios.Util.WebApi.Security.Enums;

namespace ArturRios.Util.WebApi.Security.Configuration;

/// <summary>Consolidated options controlling how <c>AuthenticationMiddleware</c> reads and validates the request token.</summary>
public class AuthenticationOptions
{
    /// <summary>Where the token is read from. Defaults to <see cref="TokenSource.Header"/>.</summary>
    public TokenSource Source { get; set; } = TokenSource.Header;

    /// <summary>The cookie name used when <see cref="Source"/> is <see cref="TokenSource.Cookie"/> or <see cref="TokenSource.Either"/>. Defaults to <c>access_token</c>.</summary>
    public string CookieName { get; set; } = "access_token";

    /// <summary>Whether the app's own HMAC JWT is accepted. Defaults to <see langword="true"/>.</summary>
    public bool EnableJwt { get; set; } = true;

    /// <summary>Whether Google ID tokens are accepted. Defaults to <see langword="false"/>. Requires <see cref="GoogleClientIds"/> and a registered <c>IAuthenticationProvider</c>.</summary>
    public bool EnableGoogle { get; set; }

    /// <summary>How the user is resolved for a valid app JWT. Defaults to <see cref="JwtValidationMode.ClaimsOnly"/>.</summary>
    public JwtValidationMode JwtMode { get; set; } = JwtValidationMode.ClaimsOnly;

    /// <summary>
    /// Whether an app JWT must carry an <c>iss</c> claim equal (ordinal) to <c>JwtConfiguration.Issuer</c>.
    /// Defaults to <see langword="false"/>: only the signature is checked, as before 5.2.0. Turn it on when the
    /// signing key is shared with another issuer, so its tokens are not accepted here. When on, a blank
    /// <c>JwtConfiguration.Issuer</c> fails the application's startup.
    /// </summary>
    public bool ValidateIssuer { get; set; }

    /// <summary>
    /// Whether an app JWT must list <c>JwtConfiguration.Audience</c> (ordinal) among its <c>aud</c> claims.
    /// Defaults to <see langword="false"/>: only the signature is checked, as before 5.2.0. Turn it on when the
    /// signing key is shared by several audiences, so a token minted for one is not accepted by another. When on,
    /// a blank <c>JwtConfiguration.Audience</c> fails the application's startup.
    /// </summary>
    public bool ValidateAudience { get; set; }

    /// <summary>The accepted Google OAuth client IDs (token audiences). Required when <see cref="EnableGoogle"/> is <see langword="true"/>.</summary>
    public IList<string> GoogleClientIds { get; set; } = new List<string>();
}
