using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Filters;
using ArturRios.Util.WebApi.Security.Interfaces;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ArturRios.Util.WebApi.Tests.Security;

[Trait("Category", "Unit")]
public class RoleRequirementFilterTests
{
    private const int AuthorizedRole = 1;
    private const int UnauthorizedRole = 2;
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private sealed record TenantUser(Guid Id, int RoleId, string TenantId) : IAuthenticatedUser;

    private static AuthorizationFilterContext BuildContext(IAuthenticatedUser? user, bool allowAnonymous)
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
    public void GivenAnAnonymousEndpointAndNoUser_WhenCheckingTheRole_ThenTheRequestIsAllowedThrough()
    {
        var context = BuildContext(null, allowAnonymous: true);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenAProtectedEndpointAndNoUser_WhenCheckingTheRole_ThenForbiddenIsReturned()
    {
        var context = BuildContext(null, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void GivenAUserInAnAuthorizedRole_WhenCheckingTheRole_ThenTheRequestIsAllowedThrough()
    {
        var user = new AuthenticatedUser(UserId, AuthorizedRole);
        var context = BuildContext(user, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenAUserInAnUnauthorizedRole_WhenCheckingTheRole_ThenForbiddenIsReturned()
    {
        var user = new AuthenticatedUser(UserId, UnauthorizedRole);
        var context = BuildContext(user, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void GivenACustomUserTypeInAnAuthorizedRole_WhenCheckingTheRole_ThenTheRequestIsAllowedThrough()
    {
        var context = BuildContext(new TenantUser(UserId, AuthorizedRole, "acme"), allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenACustomUserTypeInAnUnauthorizedRole_WhenCheckingTheRole_ThenForbiddenIsReturned()
    {
        var context = BuildContext(new TenantUser(UserId, UnauthorizedRole, "acme"), allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }
}
