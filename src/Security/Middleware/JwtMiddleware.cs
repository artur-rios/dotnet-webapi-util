using ArturRios.Configuration.Providers;
using ArturRios.Output;
using ArturRios.Util.WebApi.Api.Configuration;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace ArturRios.Util.WebApi.Security.Middleware;

public class JwtMiddleware(
    RequestDelegate next,
    SettingsProvider settings,
    IAuthenticationProvider authProvider,
    JwtTokenConfiguration tokenConfig) : WebApiMiddleware
{
    public async Task Invoke(HttpContext context)
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

        var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(' ').Last() ?? string.Empty;

        var jwtToken = JwtToken.FromToken(token, tokenConfig.Secret);
        var isValid = jwtToken.IsTokenValidAsync().GetAwaiter().GetResult();

        string? authError;

        if (isValid)
        {
            var userId = jwtToken.GetUserId();

            if (userId.HasValue)
            {
                var authenticatedUser = authProvider.GetAuthenticatedUserById(userId.Value);

                if (authenticatedUser is not null)
                {
                    context.Items["User"] = authenticatedUser;

                    await next(context);

                    return;
                }

                authError = "User not found";
            }
            else
            {
                authError = "Could not retrieve user id from token";
            }
        }
        else
        {
            authError = "Invalid token";
        }

        var output = ProcessOutput.New.WithError(authError);

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var payload = JsonConvert.SerializeObject(output);

            await context.Response.WriteAsync(payload);
        }
    }

    private bool IsSwaggerRoute(string path) =>
        settings.GetBool(AppSettingsKeys.SwaggerEnabled) is true &&
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}
