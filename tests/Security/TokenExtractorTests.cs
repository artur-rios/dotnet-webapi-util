using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Enums;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Security;

public class TokenExtractorTests
{
    private static DefaultHttpContext ContextWith(string? header, string? cookieName = null, string? cookieValue = null)
    {
        var context = new DefaultHttpContext();

        if (header is not null)
        {
            context.Request.Headers.Authorization = header;
        }

        if (cookieName is not null && cookieValue is not null)
        {
            context.Request.Headers.Cookie = $"{cookieName}={cookieValue}";
        }

        return context;
    }

    [Fact]
    public void Header_ReturnsBearerToken()
    {
        var context = ContextWith("Bearer abc.def.ghi");
        Assert.Equal("abc.def.ghi", TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Header_AcceptsLowercaseScheme()
    {
        var context = ContextWith("bearer abc.def.ghi");
        Assert.Equal("abc.def.ghi", TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Header_ReturnsEmpty_ForNonBearerScheme()
    {
        var context = ContextWith("Basic abc");
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Header_ReturnsEmpty_WhenAbsent()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void Cookie_ReturnsNamedCookie()
    {
        var context = ContextWith(header: null, cookieName: "access_token", cookieValue: "cookie.token.value");
        Assert.Equal("cookie.token.value", TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void Cookie_ReturnsEmpty_WhenAbsent()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void Cookie_IgnoresHeader()
    {
        var context = ContextWith("Bearer header.token", cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("cookie.token", TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void Either_PrefersHeader()
    {
        var context = ContextWith("Bearer header.token", cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("header.token", TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }

    [Fact]
    public void Either_FallsBackToCookie_WhenHeaderMissing()
    {
        var context = ContextWith(header: null, cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("cookie.token", TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }

    [Fact]
    public void Either_ReturnsEmpty_WhenNeitherPresent()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }
}
