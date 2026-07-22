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

    /// <summary>The accepted Google OAuth client IDs (token audiences). Required when <see cref="EnableGoogle"/> is <see langword="true"/>.</summary>
    public IList<string> GoogleClientIds { get; set; } = new List<string>();
}
