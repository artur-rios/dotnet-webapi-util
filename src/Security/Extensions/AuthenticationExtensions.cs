using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Extensions;

public static class AuthenticationExtensions
{
    public static Dictionary<string, string> ToTokenClaims(this AuthenticatedUser authenticatedUser) =>
        new() { { "id", authenticatedUser.Id.ToString() }, { "role", authenticatedUser.Role.ToString() } };
}
