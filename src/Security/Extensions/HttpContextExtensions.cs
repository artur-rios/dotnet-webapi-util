using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Extension methods for reading the user attached to the current request by
/// <see cref="Middleware.AuthenticationMiddleware"/>.</summary>
public static class HttpContextExtensions
{
    /// <summary>Gets the authenticated user attached to the request.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The authenticated user, or <see langword="null"/> if the request is not authenticated.</returns>
    public static IAuthenticatedUser? GetUser(this HttpContext context) =>
        context.Items[AuthenticationItemKeys.User] as IAuthenticatedUser;

    /// <summary>Gets the authenticated user as the app's own identity type.</summary>
    /// <typeparam name="TUser">The app's identity type.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The authenticated user, or <see langword="null"/> if the request is not authenticated or the
    /// attached user is of another type.</returns>
    public static TUser? GetUser<TUser>(this HttpContext context) where TUser : class, IAuthenticatedUser =>
        context.Items[AuthenticationItemKeys.User] as TUser;
}
