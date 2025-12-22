using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Providers;

public class TokenProvider
{
    public Authentication Provide(AuthenticatedUser user, JwtTokenConfiguration tokenConfiguration)
    {
        var jwtToken = JwtToken.FromClaims(user.ToTokenClaims(), tokenConfiguration);

        return new Authentication(jwtToken.Token, true, jwtToken.CreatedAt, jwtToken.Expiration);
    }
}
