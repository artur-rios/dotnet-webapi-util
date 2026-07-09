using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Extensions;

public static class AuthenticationExtensions
{
    public static Dictionary<string, string> ToTokenClaims(this AuthenticatedUser authenticatedUser) =>
        new()
        {
            { TokenClaimKeys.Id, authenticatedUser.Id.ToString() },
            { TokenClaimKeys.Role, authenticatedUser.Role.ToString() }
        };
}
