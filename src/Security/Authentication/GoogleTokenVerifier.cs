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
        // A token that is not a well-formed JWS at all - bad base64url, a segment that is not JSON - makes
        // GoogleJsonWebSignature throw the decoder's own exception instead of InvalidJwtException. Any such
        // token is just not a valid Google token, so it is a 401, not a 500. Failures fetching Google's keys
        // (HttpRequestException) still propagate: those are the server's problem, not the caller's.
        catch (Exception exception) when (exception is InvalidJwtException or FormatException or ArgumentException
                                              or Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }
}
