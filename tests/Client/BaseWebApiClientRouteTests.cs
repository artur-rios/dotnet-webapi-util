using System.Net.Http;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Client;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Client;

[Trait("Category", "Unit")]
public class BaseWebApiClientRouteTests
{
    private sealed class TestRoute(HttpGateway gateway) : BaseWebApiClientRoute(gateway)
    {
        public override string BaseUrl => "/test";
        public void CallAuthorize(string token) => Authorize(token);

        public Task CallAuthenticateAndAuthorizeAsync() =>
            AuthenticateAndAuthorizeAsync(new Credentials("user@example.test", "password123"), "/auth");
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private static (TestRoute Route, HttpClient Client) RouteAnswering(string json)
    {
        var httpClient = new HttpClient(new JsonHandler(json)) { BaseAddress = new Uri("https://example.test") };

        return (new TestRoute(new HttpGateway(httpClient)), httpClient);
    }

    [Fact]
    public async Task GivenAnInvalidAuthentication_WhenAuthenticatingAndAuthorizing_ThenItFailsAndNoHeaderIsSet()
    {
        var (route, httpClient) = RouteAnswering(
            """{"Token":null,"Valid":false,"CreatedAt":"","Expiration":""}""");

        await Assert.ThrowsAsync<WebApiClientException>(route.CallAuthenticateAndAuthorizeAsync);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenAValidAuthentication_WhenAuthenticatingAndAuthorizing_ThenTheTokenIsSetAsBearer()
    {
        var (route, httpClient) = RouteAnswering(
            """{"Token":"issued.jwt.token","Valid":true,"CreatedAt":"","Expiration":""}""");

        await route.CallAuthenticateAndAuthorizeAsync();

        Assert.Equal("issued.jwt.token", httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void GivenARouteAuthorizedTwice_WhenInspected_ThenOneBearerSchemeIsSet()
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
