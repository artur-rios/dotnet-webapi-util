using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArturRios.Util.WebApi.Security.Attributes;

/// <summary>Rejects requests with a 401 response unless an <see cref="AuthenticatedUser"/> was attached to the
/// context (typically by <see cref="Security.Middleware.JwtMiddleware"/>), unless the action is marked with <see cref="AllowAnonymousAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>Checks for an authenticated user on the context and short-circuits the pipeline with a 401 result if none is present.</summary>
    /// <param name="context">The authorization filter context for the current request.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();

        if (allowAnonymous)
        {
            return;
        }

        var user = (AuthenticatedUser?)context.HttpContext.Items["User"];

        if (user is null)
        {
            context.Result =
                new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }
}
