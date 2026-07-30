using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ArturRios.Util.WebApi.Tests.Security;

public class AuthorizeAttributeTests
{
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
    public void OnAuthorization_CustomUserType_DoesNotSetResult()
    {
        var context = BuildContext(new TenantUser(UserId, 1, "acme"), allowAnonymous: false);

        new AuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_NoUser_ReturnsUnauthorized()
    {
        var context = BuildContext(null, allowAnonymous: false);

        new AuthorizeAttribute().OnAuthorization(context);

        var result = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_AllowAnonymousAndNoUser_DoesNotSetResult()
    {
        var context = BuildContext(null, allowAnonymous: true);

        new AuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }
}
