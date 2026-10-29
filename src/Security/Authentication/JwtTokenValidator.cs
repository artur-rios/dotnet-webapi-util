using System.IdentityModel.Tokens.Jwt;
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
/// lookup, per <see cref="AuthenticationOptions.JwtMode"/>. The token's issuer and audience are checked against
/// <see cref="JwtConfiguration.Issuer"/> and <see cref="JwtConfiguration.Audience"/> only when
/// <see cref="AuthenticationOptions.ValidateIssuer"/> / <see cref="AuthenticationOptions.ValidateAudience"/> are on.</summary>
public class JwtTokenValidator : ITokenValidator
{
    private static readonly JwtSecurityTokenHandler TokenReader = new();

    private readonly JwtConfiguration _jwtConfig;
    private readonly JwtHandler _jwtHandler;
    private readonly IAuthenticatedUserMapper _mapper;
    private readonly AuthenticationOptions _options;

    /// <summary>Creates the validator.</summary>
    /// <param name="jwtConfig">Provides the key material used to validate the token, and the expected issuer and audience.</param>
    /// <param name="jwtHandler">Validates token signatures.</param>
    /// <param name="mapper">Interprets the token's claims as the app's user.</param>
    /// <param name="options">Controls the issuer/audience checks and how the user is resolved once the signature is valid.</param>
    /// <exception cref="InvalidOperationException"><see cref="AuthenticationOptions.ValidateIssuer"/> is on and
    /// <see cref="JwtConfiguration.Issuer"/> is blank, or <see cref="AuthenticationOptions.ValidateAudience"/> is on and
    /// <see cref="JwtConfiguration.Audience"/> is blank.</exception>
    public JwtTokenValidator(
        JwtConfiguration jwtConfig,
        JwtHandler jwtHandler,
        IAuthenticatedUserMapper mapper,
        AuthenticationOptions options)
    {
        // Checked here because the validator is created when AuthenticationMiddleware is built, at application
        // startup. A check that is on with nothing to compare against would otherwise reject every token at run
        // time instead of failing the misconfigured deployment.
        EnsureExpectedValuesConfigured(jwtConfig, options);

        _jwtConfig = jwtConfig;
        _jwtHandler = jwtHandler;
        _mapper = mapper;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAsync(string token, HttpContext context)
    {
        // Every configured key when there are any, so a token signed with a key that is no longer
        // the signing key stays valid until it is withdrawn — which is what makes replacing a signing
        // secret a rotation rather than a cutover that invalidates every token in flight. Falls back
        // to the single secret when no keys are configured, which is what every configuration written
        // before ArturRios.Jwt 1.1.0 looks like.
        var isValid = _jwtConfig.Keys.Count > 0
            ? await _jwtHandler.IsTokenValidAsync(token, _jwtConfig.Keys)
            : await _jwtHandler.IsTokenValidAsync(token, _jwtConfig.Secret);

        if (!isValid)
        {
            return new TokenValidationResult(null, "Invalid token");
        }

        // Only after the signature: before it, the claims are whatever the sender wrote.
        var issuerAndAudienceError = CheckIssuerAndAudience(token);

        if (issuerAndAudienceError is not null)
        {
            return new TokenValidationResult(null, issuerAndAudienceError);
        }

        var claims = TokenClaimsReader.Read(token);

        if (claims is null)
        {
            return new TokenValidationResult(null, "Could not read token claims");
        }

        if (_options.JwtMode == JwtValidationMode.ClaimsOnly)
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

    private string? CheckIssuerAndAudience(string token)
    {
        if (!_options.ValidateIssuer && !_options.ValidateAudience)
        {
            return null;
        }

        JwtSecurityToken jwt;

        try
        {
            jwt = TokenReader.ReadJwtToken(token);
        }
        catch (Exception)
        {
            return "Could not read token claims";
        }

        if (_options.ValidateIssuer && !string.Equals(jwt.Issuer, _jwtConfig.Issuer, StringComparison.Ordinal))
        {
            return "Invalid token issuer";
        }

        if (_options.ValidateAudience && !jwt.Audiences.Contains(_jwtConfig.Audience, StringComparer.Ordinal))
        {
            return "Invalid token audience";
        }

        return null;
    }

    private static void EnsureExpectedValuesConfigured(JwtConfiguration jwtConfig, AuthenticationOptions options)
    {
        if (options.ValidateIssuer && string.IsNullOrWhiteSpace(jwtConfig.Issuer))
        {
            throw new InvalidOperationException(
                "AuthenticationOptions.ValidateIssuer is on, but JwtConfiguration.Issuer is empty. Configure the " +
                "issuer the app's tokens are minted with, or turn ValidateIssuer off.");
        }

        if (options.ValidateAudience && string.IsNullOrWhiteSpace(jwtConfig.Audience))
        {
            throw new InvalidOperationException(
                "AuthenticationOptions.ValidateAudience is on, but JwtConfiguration.Audience is empty. Configure " +
                "the audience the app's tokens are minted for, or turn ValidateAudience off.");
        }
    }

    private IAuthenticatedUser? MapUser(IReadOnlyDictionary<string, string> claims)
    {
        try
        {
            return _mapper.FromClaims(claims);
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
            return _mapper.IdFromClaims(claims);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
