using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Validates a Google ID token via <see cref="IGoogleTokenVerifier"/> and resolves the app user by the token's verified email through <see cref="IAuthenticationProvider"/>.</summary>
/// <param name="verifier">Verifies the Google ID token against the configured client IDs.</param>
/// <param name="options">Supplies the accepted Google client IDs (audiences).</param>
public class GoogleTokenValidator(IGoogleTokenVerifier verifier, AuthenticationOptions options) : ITokenValidator
{
    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        var payload = await verifier.VerifyAsync(token, options.GoogleClientIds);

        if (payload is null)
        {
            return new TokenValidationResult(null, "Invalid Google token");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return new TokenValidationResult(null, "Google token has no email");
        }

        if (!payload.EmailVerified)
        {
            return new TokenValidationResult(null, "Google email not verified");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserByEmail(payload.Email);

        return new TokenValidationResult(user, user is null ? "User not found" : null);
    }
}
