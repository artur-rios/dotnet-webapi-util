using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Google.Apis.Auth;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Default <see cref="IGoogleTokenVerifier"/> backed by <see cref="GoogleJsonWebSignature"/>, which fetches and caches Google's signing keys and checks signature, issuer, expiry, and audience.</summary>
public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    /// <inheritdoc />
    public async Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings { Audience = audiences };
            var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);

            return new GoogleTokenPayload(payload.Email ?? string.Empty, payload.Subject ?? string.Empty, payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
