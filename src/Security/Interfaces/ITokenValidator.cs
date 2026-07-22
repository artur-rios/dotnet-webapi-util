using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>Validates a raw authentication token and, on success, resolves the <see cref="Records.AuthenticatedUser"/> it represents.</summary>
public interface ITokenValidator
{
    /// <summary>Attempts to validate <paramref name="token"/> and resolve its user. Returns a result whose <c>User</c> is
    /// <see langword="null"/> (with an <c>Error</c>) when this validator does not accept the token, so the caller can try the next validator.</summary>
    /// <param name="token">The raw token extracted from the request. May be empty.</param>
    /// <param name="context">The current HTTP context, used to resolve request-scoped services such as <c>IAuthenticationProvider</c>.</param>
    Task<TokenValidationResult> ValidateAsync(string token, HttpContext context);
}
