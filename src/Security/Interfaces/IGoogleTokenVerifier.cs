using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Verifies a Google ID token against the accepted audiences and returns its verified payload, or <see langword="null"/> when the token is not valid.</summary>
public interface IGoogleTokenVerifier
{
    /// <summary>Verifies <paramref name="token"/> (signature, issuer, expiry, and audience against <paramref name="audiences"/>).</summary>
    /// <returns>The verified payload, or <see langword="null"/> if the token is invalid or rejected.</returns>
    Task<GoogleTokenPayload?> VerifyAsync(string token, IEnumerable<string> audiences);
}
