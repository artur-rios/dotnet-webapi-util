using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Extension methods for converting authentication-related types.</summary>
public static class AuthenticationExtensions
{
    /// <summary>Converts an <see cref="AuthenticatedUser"/> into the claim dictionary (keyed by <see cref="TokenClaimKeys"/>) used to build a JWT.</summary>
    /// <param name="authenticatedUser">The authenticated user to convert.</param>
    /// <returns>A dictionary containing the user's id and role claims.</returns>
    public static Dictionary<string, string> ToTokenClaims(this AuthenticatedUser authenticatedUser) =>
        new()
        {
            { TokenClaimKeys.Id, authenticatedUser.Id.ToString() },
            { TokenClaimKeys.Role, authenticatedUser.Role.ToString() }
        };
}
