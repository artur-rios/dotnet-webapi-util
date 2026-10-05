using ArturRios.Extensions;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArturRios.Util.WebApi.Security.Filters;

/// <summary>Authorization filter that rejects requests with a 403 response unless the authenticated user's role
/// is one of <paramref name="authorizedRoles"/>, or with a 401 response when no user is authenticated. Applied
/// declaratively via <see cref="Attributes.RoleRequirementAttribute"/>, unless the action is marked with
/// <see cref="AllowAnonymousAttribute"/>.</summary>
/// <param name="authorizedRoles">The role values permitted to access the resource.</param>
public class RoleRequirementFilter(params int[] authorizedRoles) : IAuthorizationFilter
{
    /// <summary>Checks the authenticated user's role against the authorized roles and short-circuits the pipeline
    /// with a 401 result when there is no user, or a 403 result when the role does not match.</summary>
    /// <param name="context">The authorization filter context for the current request.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.AllowsAnonymous())
        {
            return;
        }

        var user = context.HttpContext.GetUser();

        if (user is null)
        {
            context.Result = AuthorizationResults.Unauthorized();

            return;
        }

        if (!user.RoleId.In(authorizedRoles))
        {
            context.Result = AuthorizationResults.Forbidden();
        }
    }
}
