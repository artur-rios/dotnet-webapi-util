using ArturRios.Util.WebApi.Security.Authentication;
using ArturRios.Util.WebApi.Security.Enums;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Tests.Security;

[Trait("Category", "Unit")]
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
    public void GivenABearerHeader_WhenExtractingFromTheHeader_ThenTheTokenComesBack()
    {
        var context = ContextWith("Bearer abc.def.ghi");
        Assert.Equal("abc.def.ghi", TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void GivenALowercaseBearerScheme_WhenExtractingFromTheHeader_ThenTheTokenComesBack()
    {
        var context = ContextWith("bearer abc.def.ghi");
        Assert.Equal("abc.def.ghi", TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void GivenANonBearerScheme_WhenExtractingFromTheHeader_ThenNothingComesBack()
    {
        var context = ContextWith("Basic abc");
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void GivenNoAuthorizationHeader_WhenExtractingFromTheHeader_ThenNothingComesBack()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Header, "access_token"));
    }

    [Fact]
    public void GivenTheNamedCookie_WhenExtractingFromTheCookie_ThenTheTokenComesBack()
    {
        var context = ContextWith(header: null, cookieName: "access_token", cookieValue: "cookie.token.value");
        Assert.Equal("cookie.token.value", TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void GivenNoCookie_WhenExtractingFromTheCookie_ThenNothingComesBack()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void GivenTheCookieSourceAndAHeaderToken_WhenExtracting_ThenTheHeaderIsIgnored()
    {
        var context = ContextWith("Bearer header.token", cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("cookie.token", TokenExtractor.Extract(context, TokenSource.Cookie, "access_token"));
    }

    [Fact]
    public void GivenBothAHeaderAndACookie_WhenExtractingFromEither_ThenTheHeaderWins()
    {
        var context = ContextWith("Bearer header.token", cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("header.token", TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }

    [Fact]
    public void GivenOnlyACookie_WhenExtractingFromEither_ThenTheCookieIsUsed()
    {
        var context = ContextWith(header: null, cookieName: "access_token", cookieValue: "cookie.token");
        Assert.Equal("cookie.token", TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }

    [Fact]
    public void GivenNeitherAHeaderNorACookie_WhenExtractingFromEither_ThenNothingComesBack()
    {
        var context = ContextWith(header: null);
        Assert.Equal(string.Empty, TokenExtractor.Extract(context, TokenSource.Either, "access_token"));
    }
}
