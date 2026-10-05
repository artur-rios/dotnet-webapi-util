using System.Net.Http.Headers;
using ArturRios.Util.WebApi.Security.Constants;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Extension methods for reading the authentication token of the current request and the user attached
/// to it by <see cref="Middleware.AuthenticationMiddleware"/>.</summary>
public static class HttpContextExtensions
{
    private const string BearerScheme = "Bearer";

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

    /// <summary>Extracts the raw authentication token from the <c>Authorization: Bearer</c> header, the named
    /// cookie, or either (header first).</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="source">Where to read the token from.</param>
    /// <param name="cookieName">The cookie name used when <paramref name="source"/> is <see cref="TokenSource.Cookie"/>
    /// or <see cref="TokenSource.Either"/>.</param>
    /// <returns>The token, or <see cref="string.Empty"/> when none is found.</returns>
    public static string ExtractToken(this HttpContext context, TokenSource source, string cookieName)
    {
        ArgumentNullException.ThrowIfNull(context);

        return source switch
        {
            TokenSource.Header => TokenFromHeader(context),
            TokenSource.Cookie => TokenFromCookie(context, cookieName),
            TokenSource.Either => TokenFromHeader(context) is { Length: > 0 } header
                ? header
                : TokenFromCookie(context, cookieName),
            _ => string.Empty
        };
    }

    /// <summary>Attaches <paramref name="user"/> to the request, where <see cref="GetUser"/> and the authorization
    /// filters look for it.</summary>
    internal static void SetUser(this HttpContext context, IAuthenticatedUser user) =>
        context.Items[AuthenticationItemKeys.User] = user;

    private static string TokenFromHeader(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header) || !AuthenticationHeaderValue.TryParse(header, out var parsed) ||
            !string.Equals(parsed.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return string.Empty;
        }

        return parsed.Parameter.Trim();
    }

    private static string TokenFromCookie(HttpContext context, string cookieName)
    {
        var value = context.Request.Cookies[cookieName];

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
