using System.Net.Http;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Client;

namespace ArturRios.Util.WebApi.Tests.Client;

public class BaseWebApiClientRouteTests
{
    private sealed class TestRoute(HttpGateway gateway) : BaseWebApiClientRoute(gateway)
    {
        public override string BaseUrl => "/test";
        public void CallAuthorize(string token) => Authorize(token);
    }

    [Fact]
    public void Authorize_IsIdempotent_AndSetsBearerScheme()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://example.test") };
        var route = new TestRoute(new HttpGateway(httpClient));

        route.CallAuthorize("token-one");
        route.CallAuthorize("token-two");

        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("token-two", httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }
}
