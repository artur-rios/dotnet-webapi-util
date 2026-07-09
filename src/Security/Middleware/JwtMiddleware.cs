using ArturRios.Configuration.Providers;
using ArturRios.Jwt;
using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Enums;
using ArturRios.Util.WebApi.Security.Factories;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace ArturRios.Util.WebApi.Security.Middleware;

/// <summary>
/// Authenticates requests by validating the bearer JSON Web Token and attaching the resulting
/// <see cref="AuthenticatedUser"/> to <c>HttpContext.Items["User"]</c>. Swagger and
/// <see cref="AllowAnonymousAttribute"/> endpoints are skipped. How the user is resolved is
/// governed by <see cref="JwtAuthenticationOptions.ValidationMode"/>.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="settings">Configuration used to detect Swagger routes.</param>
/// <param name="tokenConfig">The JWT configuration providing the signing secret.</param>
/// <param name="jwtHandler">Validates token signatures and reads claims.</param>
/// <param name="options">
/// Controls how the authenticated user is resolved. When omitted, defaults to
/// <see cref="JwtValidationMode.ClaimsOnly"/> (stateless, no data-store lookup).
/// </param>
public class JwtMiddleware(
    RequestDelegate next,
    SettingsProvider settings,
    JwtConfiguration tokenConfig,
    JwtHandler jwtHandler,
    JwtAuthenticationOptions? options = null) : WebApiMiddleware
{
    private readonly JwtAuthenticationOptions _options = options ?? new JwtAuthenticationOptions();

    /// <summary>
    /// Validates the request's bearer token and, on success, attaches the authenticated user before
    /// invoking the next middleware; otherwise writes a 401 response.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        var skipRoute =
            IsSwaggerRoute(context.Request.Path.Value ?? string.Empty) ||
            endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null;

        if (skipRoute)
        {
            await next(context);

            return;
        }

        var token = ExtractBearerToken(context);

        var isValid = await jwtHandler.IsTokenValidAsync(token, tokenConfig.Secret);

        if (!isValid)
        {
            await WriteUnauthorized(context, "Invalid token");

            return;
        }

        var (authenticatedUser, authError) = ResolveUser(context, token);

        if (authenticatedUser is not null)
        {
            context.Items["User"] = authenticatedUser;

            await next(context);

            return;
        }

        await WriteUnauthorized(context, authError);
    }

    private (AuthenticatedUser? User, string? Error) ResolveUser(HttpContext context, string token)
    {
        if (_options.ValidationMode == JwtValidationMode.ClaimsOnly)
        {
            var claimsUser = AuthenticatedUserFactory.FromToken(token);

            return (claimsUser, claimsUser is null ? "Could not retrieve user from token" : null);
        }

        var userId = jwtHandler.GetUserIdFromToken(token);

        if (!userId.HasValue)
        {
            return (null, "Could not retrieve user id from token");
        }

        var authProvider = context.RequestServices.GetRequiredService<IAuthenticationProvider>();
        var user = authProvider.GetAuthenticatedUserById(userId.Value);

        return (user, user is null ? "User not found" : null);
    }

    private static async Task WriteUnauthorized(HttpContext context, string? authError)
    {
        var output = ProcessOutput.New.WithError(authError ?? "Unauthorized");

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var payload = JsonConvert.SerializeObject(output);

        await context.Response.WriteAsync(payload);
    }

    private static string ExtractBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header) || !AuthenticationHeaderValue.TryParse(header, out var parsed))
        {
            return string.Empty;
        }

        if (!string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return string.Empty;
        }

        return parsed.Parameter.Trim();
    }

    private bool IsSwaggerRoute(string path) =>
        settings.GetBool(AppSettingsKeys.SwaggerEnabled) is true &&
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}
