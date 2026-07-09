using ArturRios.Extensions;
using ArturRios.Output;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArturRios.Util.WebApi.Security.Filters;

/// <summary>Authorization filter that rejects requests with a 403 response unless the authenticated user's role
/// is one of <paramref name="authorizedRoles"/>. Applied declaratively via <see cref="Attributes.RoleRequirementAttribute"/>,
/// unless the action is marked with <see cref="AllowAnonymousAttribute"/>.</summary>
/// <param name="authorizedRoles">The role values permitted to access the resource.</param>
public class RoleRequirementFilter(params int[] authorizedRoles) : IAuthorizationFilter
{
    /// <summary>Checks the authenticated user's role against the authorized roles and short-circuits the pipeline with a 403 result if it does not match.</summary>
    /// <param name="context">The authorization filter context for the current request.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();

        if (allowAnonymous)
        {
            return;
        }

        var user = context.HttpContext.Items["User"] as AuthenticatedUser;

        var authorized = false;

        if (user is not null)
        {
            authorized = user.Role.In(authorizedRoles);
        }

        if (authorized)
        {
            return;
        }

        var output = ProcessOutput.New.WithError("You do not have permission to access this resource");

        context.Result = new ObjectResult(output) { StatusCode = HttpStatusCodes.Forbidden };
    }
}
