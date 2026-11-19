using ArturRios.Util.WebApi.Security.Attributes;
using ArturRios.Util.WebApi.Security.Filters;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Security.Attributes;

[Trait("Category", "Unit")]
public class RoleRequirementAttributeTests
{
    private static AuthorizationFilterContext ContextForRole(int roleId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = new AuthenticatedUser(Guid.NewGuid(), roleId);

        return new AuthorizationFilterContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    [Fact]
    public void GivenAuthorizedRoles_WhenTheAttributeIsCreated_ThenItDescribesARoleRequirementFilterWithThoseRoles()
    {
        var attribute = new RoleRequirementAttribute(1, 3);

        Assert.Equal(typeof(RoleRequirementFilter), attribute.ImplementationType);
        Assert.Equal([1, 3], Assert.IsType<int[]>(Assert.Single(attribute.Arguments!)));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public void GivenTheAttribute_WhenItsFilterIsCreated_ThenOnlyTheAuthorizedRolesGetThrough(int roleId, bool allowed)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var filter = Assert.IsType<RoleRequirementFilter>(new RoleRequirementAttribute(1, 3).CreateInstance(services));
        var context = ContextForRole(roleId);

        filter.OnAuthorization(context);

        if (allowed)
        {
            Assert.Null(context.Result);
        }
        else
        {
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        }
    }

    [Fact]
    public void GivenNoAuthorizedRoles_WhenAUserIsChecked_ThenItIsForbidden()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var filter = Assert.IsType<RoleRequirementFilter>(new RoleRequirementAttribute().CreateInstance(services));
        var context = ContextForRole(1);

        filter.OnAuthorization(context);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }
}
