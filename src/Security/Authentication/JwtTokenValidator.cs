using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Validates the app's own HMAC-signed JWT and resolves the user through the registered
/// <see cref="IAuthenticatedUserMapper"/> — from claims alone or by an <see cref="IAuthenticationProvider"/>
/// lookup, per <see cref="AuthenticationOptions.JwtMode"/>.</summary>
/// <param name="jwtConfig">Provides the signing secret used to validate the token.</param>
/// <param name="jwtHandler">Validates token signatures.</param>
/// <param name="mapper">Interprets the token's claims as the app's user.</param>
/// <param name="options">Controls how the user is resolved once the signature is valid.</param>
public class JwtTokenValidator(
    JwtConfiguration jwtConfig,
    JwtHandler jwtHandler,
    IAuthenticatedUserMapper mapper,
    AuthenticationOptions options) : ITokenValidator
{
    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        var isValid = await jwtHandler.IsTokenValidAsync(token, jwtConfig.Secret);

        if (!isValid)
        {
            return new TokenValidationResult(null, "Invalid token");
        }

        var claims = TokenClaimsReader.Read(token);

        if (claims is null)
        {
            return new TokenValidationResult(null, "Could not read token claims");
        }

        if (options.JwtMode == JwtValidationMode.ClaimsOnly)
        {
            var claimsUser = MapUser(claims);

            return new TokenValidationResult(claimsUser, claimsUser is null ? "Could not retrieve user from token" : null);
        }

        var userId = MapId(claims);

        if (userId is null)
        {
            return new TokenValidationResult(null, "Could not retrieve user id from token");
        }

        var provider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = provider.GetAuthenticatedUserById(userId.Value);

        return new TokenValidationResult(user, user is null ? "User not found" : null);
    }

    private IAuthenticatedUser? MapUser(IReadOnlyDictionary<string, string> claims)
    {
        try
        {
            return mapper.FromClaims(claims);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Guid? MapId(IReadOnlyDictionary<string, string> claims)
    {
        try
        {
            return mapper.IdFromClaims(claims);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
