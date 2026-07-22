using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Factories;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Validates the app's own HMAC-signed JWT and resolves the user by claims or by an <see cref="IAuthenticationProvider"/> lookup, per <see cref="AuthenticationOptions.JwtMode"/>.</summary>
/// <param name="jwtConfig">Provides the signing secret used to validate the token.</param>
/// <param name="jwtHandler">Validates signatures and reads the user id claim.</param>
/// <param name="options">Controls how the user is resolved once the signature is valid.</param>
public class JwtTokenValidator(JwtConfiguration jwtConfig, JwtHandler jwtHandler, AuthenticationOptions options) : ITokenValidator
{
    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        var isValid = await jwtHandler.IsTokenValidAsync(token, jwtConfig.Secret);

        if (!isValid)
        {
            return new TokenValidationResult(null, "Invalid token");
        }

        if (options.JwtMode == JwtValidationMode.ClaimsOnly)
        {
            var claimsUser = AuthenticatedUserFactory.FromToken(token);

            return new TokenValidationResult(claimsUser, claimsUser is null ? "Could not retrieve user from token" : null);
        }

        var userId = jwtHandler.GetUserIdFromToken(token);

        if (!userId.HasValue)
        {
            return new TokenValidationResult(null, "Could not retrieve user id from token");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserById(userId.Value);

        return new TokenValidationResult(user, user is null ? "User not found" : null);
    }
}
