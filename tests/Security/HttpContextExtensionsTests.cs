using ArturRios.Util.WebApi.Security.Extensions;
using ArturRios.Util.WebApi.Security.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Security;

[Trait("Category", "Unit")]
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
    public void GivenAUserIsAttached_WhenReadingIt_ThenTheInterfaceComesBack()
    {
        var context = ContextWithUser(new TestUser(UserId, 3));

        var user = context.GetUser();

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(3, user.RoleId);
    }

    [Fact]
    public void GivenNoUserIsAttached_WhenReadingIt_ThenNullComesBack()
    {
        Assert.Null(ContextWithUser(null).GetUser());
    }

    [Fact]
    public void GivenTheAttachedUserMatchesTheRequestedType_WhenReadingIt_ThenTheConcreteTypeComesBack()
    {
        var context = ContextWithUser(new TestUser(UserId, 3));

        var user = context.GetUser<TestUser>();

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
    }

    [Fact]
    public void GivenTheAttachedUserIsADifferentType_WhenReadingIt_ThenNullComesBack()
    {
        var context = ContextWithUser(new OtherUser(UserId, 3));

        Assert.Null(context.GetUser<TestUser>());
    }

    [Fact]
    public void GivenNoUserIsAttached_WhenReadingItAsAConcreteType_ThenNullComesBack()
    {
        Assert.Null(ContextWithUser(null).GetUser<TestUser>());
    }
}
