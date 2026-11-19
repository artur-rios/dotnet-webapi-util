using System.Net;
using ArturRios.Util.WebApi.Extensions;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Extensions;

[Trait("Category", "Unit")]
public class HttpContextClientExtensionsTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("::1", "::1")]
    public void GivenARemoteAddress_WhenReadingTheClientIp_ThenItComesBackInItsShortestForm(string remote, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);

        Assert.Equal(expected, context.GetClientIpAddress());
    }

    [Fact]
    public void GivenNoRemoteAddress_WhenReadingTheClientIp_ThenNullComesBack()
    {
        Assert.Null(new DefaultHttpContext().GetClientIpAddress());
    }

    [Fact]
    public void GivenAnXForwardedForHeader_WhenReadingTheClientIp_ThenTheHeaderIsNotTrusted()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.9";

        Assert.Equal("10.0.0.1", context.GetClientIpAddress());
    }
}
