using ArturRios.Configuration.Providers;
using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Extensions;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Security.Middleware;

/// <summary>
/// Authenticates requests by extracting one token (from the header, a cookie, or either, per
/// <see cref="AuthenticationOptions.Source"/>) and running it through the enabled
/// <see cref="ITokenValidator"/>s in order; the first that resolves a user attaches it to
/// <c>HttpContext.Items["User"]</c>. Swagger and <see cref="AllowAnonymousAttribute"/> endpoints are skipped.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="settings">Configuration used to detect Swagger routes.</param>
/// <param name="options">Controls the token source and which validators run.</param>
/// <param name="validators">The enabled validators, tried in registration order (app JWT first, Google second).</param>
public class AuthenticationMiddleware(
    RequestDelegate next,
    SettingsProvider settings,
    AuthenticationOptions options,
    IEnumerable<ITokenValidator> validators) : WebApiMiddleware
{
    private const string MissingTokenError = "Authentication token not provided";
    private const string UnauthorizedError = "Unauthorized";

    private readonly ITokenValidator[] _validators = validators.ToArray();

    /// <summary>Validates the request token and, on success, attaches the authenticated user before invoking the next middleware; otherwise writes a 401 response.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint().AllowsAnonymous() || IsSwaggerRoute(context.Request.Path))
        {
            await next(context);

            return;
        }

        var token = context.ExtractToken(options.Source, options.CookieName);

        string? lastError = null;

        foreach (var validator in _validators)
        {
            var (user, error) = await validator.ValidateAsync(token, context);

            if (user is not null)
            {
                context.SetUser(user);

                await next(context);

                return;
            }

            lastError = error;
        }

        // Validators still run without a token (the contract allows it, for schemes that read something
        // else off the request); the error just says plainly what was missing.
        await WriteUnauthorized(context, string.IsNullOrEmpty(token) ? MissingTokenError : lastError);
    }

    private static Task WriteUnauthorized(HttpContext context, string? authError) =>
        context.Response.HasStarted
            ? Task.CompletedTask
            : context.Response.WriteOutputAsync(StatusCodes.Status401Unauthorized,
                ProcessOutput.New.WithError(authError ?? UnauthorizedError));

    private bool IsSwaggerRoute(PathString path) =>
        path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) &&
        settings.GetBool(AppSettingsKeys.SwaggerEnabled) is true;
}
