using ArturRios.Configuration.Providers;
using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Configuration;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

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
    private readonly ITokenValidator[] _validators = validators.ToArray();

    /// <summary>Validates the request token and, on success, attaches the authenticated user before invoking the next middleware; otherwise writes a 401 response.</summary>
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

        var token = TokenExtractor.Extract(context, options.Source, options.CookieName);

        string? lastError = null;

        foreach (var validator in _validators)
        {
            var (user, error) = await validator.ValidateAsync(token, context);

            if (user is not null)
            {
                context.Items["User"] = user;

                await next(context);

                return;
            }

            lastError = error;
        }

        await WriteUnauthorized(context, lastError);
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

    private bool IsSwaggerRoute(string path) =>
        settings.GetBool(AppSettingsKeys.SwaggerEnabled) is true &&
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}
