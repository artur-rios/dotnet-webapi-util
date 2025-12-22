using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Interfaces;

public interface IAuthenticationProvider
{
    AuthenticatedUser? GetAuthenticatedUserById(int id);
}
