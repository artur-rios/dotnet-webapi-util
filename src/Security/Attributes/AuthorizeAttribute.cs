using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Filters;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArturRios.Util.WebApi.Security.Attributes;

/// <summary>Rejects requests with a 401 response unless an <see cref="IAuthenticatedUser"/> was attached to the
/// context (typically by <see cref="Security.Middleware.AuthenticationMiddleware"/>), unless the action is marked with <see cref="AllowAnonymousAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>Checks for an authenticated user on the context and short-circuits the pipeline with a 401 result
    /// carrying a failed <c>ProcessOutput</c> envelope if none is present.</summary>
    /// <param name="context">The authorization filter context for the current request.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.AllowsAnonymous() || context.HttpContext.GetUser() is not null)
        {
            return;
        }

        context.Result = AuthorizationResults.Unauthorized();
    }
}
