using System.Net.Http.Headers;
using ArturRios.Util.WebApi.Security.Enums;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Authentication;

/// <summary>Reads the raw authentication token from an <see cref="HttpContext"/> according to a <see cref="TokenSource"/>.</summary>
public static class TokenExtractor
{
    /// <summary>Extracts the token from the header, the named cookie, or either (header first), returning <see cref="string.Empty"/> when none is found.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="source">Where to read the token from.</param>
    /// <param name="cookieName">The cookie name used when <paramref name="source"/> is <see cref="TokenSource.Cookie"/> or <see cref="TokenSource.Either"/>.</param>
    public static string Extract(HttpContext context, TokenSource source, string cookieName)
    {
        ArgumentNullException.ThrowIfNull(context);

        return source switch
        {
            TokenSource.Header => FromHeader(context),
            TokenSource.Cookie => FromCookie(context, cookieName),
            TokenSource.Either => FromHeader(context) is { Length: > 0 } header ? header : FromCookie(context, cookieName),
            _ => string.Empty
        };
    }

    private static string FromHeader(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header) || !AuthenticationHeaderValue.TryParse(header, out var parsed) ||
            !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return string.Empty;
        }

        return parsed.Parameter.Trim();
    }

    private static string FromCookie(HttpContext context, string cookieName)
    {
        var value = context.Request.Cookies[cookieName];

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
