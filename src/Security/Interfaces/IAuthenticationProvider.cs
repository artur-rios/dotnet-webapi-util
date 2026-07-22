using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Resolves an <see cref="AuthenticatedUser"/> by id or email, used by the enabled <see cref="ITokenValidator"/>s
/// (via <see cref="Security.Middleware.AuthenticationMiddleware"/>) when validation requires a data-store lookup rather than trusting token claims alone.</summary>
public interface IAuthenticationProvider
{
    /// <summary>Looks up the authenticated user with the given id.</summary>
    /// <param name="id">The user id, typically extracted from the JWT.</param>
    /// <returns>The matching <see cref="AuthenticatedUser"/>, or <c>null</c> if none was found.</returns>
    AuthenticatedUser? GetAuthenticatedUserById(int id);

    /// <summary>Looks up the authenticated user with the given email, used when resolving an external (e.g. Google) identity.</summary>
    /// <param name="email">The verified email from the external token.</param>
    /// <returns>The matching <see cref="AuthenticatedUser"/>, or <c>null</c> if none was found.</returns>
    AuthenticatedUser? GetAuthenticatedUserByEmail(string email);
}
