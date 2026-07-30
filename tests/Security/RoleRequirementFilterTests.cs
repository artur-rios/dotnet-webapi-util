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
        var user = new AuthenticatedUser(UserId, AuthorizedRole);
        var context = BuildContext(user, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_NoAllowAnonymousAndUnauthorizedRole_ReturnsForbidden()
    {
        var user = new AuthenticatedUser(UserId, UnauthorizedRole);
        var context = BuildContext(user, allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_CustomUserTypeWithAuthorizedRole_DoesNotSetResult()
    {
        var context = BuildContext(new TenantUser(UserId, AuthorizedRole, "acme"), allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_CustomUserTypeWithUnauthorizedRole_ReturnsForbidden()
    {
        var context = BuildContext(new TenantUser(UserId, UnauthorizedRole, "acme"), allowAnonymous: false);
        var filter = new RoleRequirementFilter(AuthorizedRole);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }
}
