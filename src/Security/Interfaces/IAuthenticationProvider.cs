namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Resolves an <see cref="IAuthenticatedUser"/> by id or email, used by the enabled <see cref="ITokenValidator"/>s
/// (via <see cref="Security.Middleware.AuthenticationMiddleware"/>) when validation requires a data-store lookup rather than trusting token claims alone.</summary>
public interface IAuthenticationProvider
{
    /// <summary>Looks up the authenticated user with the given id.</summary>
    /// <param name="id">The user id, typically read from the token by the app's mapper.</param>
    /// <returns>The matching user, or <c>null</c> if none was found.</returns>
    IAuthenticatedUser? GetAuthenticatedUserById(Guid id);

    /// <summary>Looks up the authenticated user with the given email, used when resolving an external (e.g. Google) identity.</summary>
    /// <param name="email">The verified email from the external token.</param>
    /// <returns>The matching user, or <c>null</c> if none was found.</returns>
    IAuthenticatedUser? GetAuthenticatedUserByEmail(string email);
}
