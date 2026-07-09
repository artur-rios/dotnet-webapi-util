using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Filters;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ArturRios.Util.WebApi.Tests.Security;

public class RoleRequirementFilterTests
{
    private const int AuthorizedRole = 1;
    private const int UnauthorizedRole = 2;

    private static AuthorizationFilterContext BuildContext(AuthenticatedUser? user, bool allowAnonymous)
    {
        var httpContext = new DefaultHttpContext();

        if (user is not null)
        {
            httpContext.Items["User"] = user;
        }

        var actionDescriptor = new ActionDescriptor();

        if (allowAnonymous)
        {
            actionDescriptor.EndpointMetadata = new List<object> { new AllowAnonymousAttribute() };
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void OnAuthorization_AllowAnonymousAndNullUser_DoesNotSetResult()
    {
        var context = BuildContext(null, allowAnonymous: true);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_NoAllowAnonymousAndNullUser_ReturnsForbidden()
    {
        var context = BuildContext(null, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_NoAllowAnonymousAndAuthorizedRole_DoesNotSetResult()
    {
        var user = new AuthenticatedUser(1, AuthorizedRole);
        var context = BuildContext(user, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_NoAllowAnonymousAndUnauthorizedRole_ReturnsForbidden()
    {
        var user = new AuthenticatedUser(1, UnauthorizedRole);
        var context = BuildContext(user, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }
}
