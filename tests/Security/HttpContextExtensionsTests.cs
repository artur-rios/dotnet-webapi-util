using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Security;

public class HttpContextExtensionsTests
{
    private static readonly Guid UserId = Guid.Parse("3f2a9c1e-7b64-4d0a-9f11-8c5d2e6a4b90");

    private sealed record TestUser(Guid Id, int RoleId) : IAuthenticatedUser;

    private sealed record OtherUser(Guid Id, int RoleId) : IAuthenticatedUser;

    private static HttpContext ContextWithUser(IAuthenticatedUser? user)
    {
        var context = new DefaultHttpContext();

        if (user is not null)
        {
            context.Items["User"] = user;
        }

        return context;
    }

    [Fact]
    public void GetUser_ReturnsInterface_WhenUserAttached()
    {
        var context = ContextWithUser(new TestUser(UserId, 3));

        var user = context.GetUser();

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void GetUser_ReturnsNull_WhenNoUserAttached()
    {
        Assert.Null(ContextWithUser(null).GetUser());
    }

    [Fact]
    public void GetUserOfT_ReturnsConcreteType_WhenTypesMatch()
    {
        var context = ContextWithUser(new TestUser(UserId, 3));

        var user = context.GetUser<TestUser>();

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
    }

    [Fact]
    public void GetUserOfT_ReturnsNull_WhenTypeDoesNotMatch()
    {
        var context = ContextWithUser(new OtherUser(UserId, 3));

        Assert.Null(context.GetUser<TestUser>());
    }

    [Fact]
    public void GetUserOfT_ReturnsNull_WhenNoUserAttached()
    {
        Assert.Null(ContextWithUser(null).GetUser<TestUser>());
    }
}
